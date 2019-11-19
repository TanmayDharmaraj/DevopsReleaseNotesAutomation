using Html2Markdown;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Core.WebApi.Types;
using Microsoft.TeamFoundation.Wiki.WebApi;
using Microsoft.TeamFoundation.Work.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DevOpsReleaseNotesAutomation
{
    public class CreateReleaseNotes
	{
		private Uri _uri;
		private string _project;
		static string _wikiName;
		static string _teamName;
		private readonly IConfiguration _configuration;
		private VssBasicCredential credentials;

		public CreateReleaseNotes(IConfiguration configuration)
		{
			_configuration = configuration;
			if (string.IsNullOrEmpty(_configuration["PersonalAccessToken"]))
			{
				throw new ApplicationException("The credential for DevOps is not found.");
			}
			credentials = new VssBasicCredential("", _configuration["PersonalAccessToken"]);

			if (string.IsNullOrEmpty(_configuration["DevOpsUri"]))
			{
				throw new ApplicationException("The URL for Azure DevOps is not found.");
			}
			bool isSuccess = Uri.TryCreate(_configuration["DevOpsUri"], UriKind.Absolute, out _uri);
			if (!isSuccess)
			{
				throw new ApplicationException("Could not parse URI for Azure Dev Ops");
			}

			if (string.IsNullOrEmpty(_configuration["DevOpsProjectName"]))
			{
				throw new ApplicationException("The project name for DevOps is not found.");
			}
			_project = _configuration["DevOpsProjectName"];

			if (string.IsNullOrEmpty(_configuration["DevOpsWikiName"]))
			{
				throw new ApplicationException("The Wiki Repo name for DevOps is not found.");
			}
			_wikiName = _configuration["DevOpsWikiName"];

			if (string.IsNullOrEmpty(_configuration["DevOpsTeamName"]))
			{
				throw new ApplicationException("The team name for project is not found.");
			}
			_teamName = _configuration["DevOpsTeamName"];
		}

		[FunctionName("CreateReleaseNotes")]
#if DEBUG
		public async Task Run([HttpTrigger] string input, ILogger log)
#else
		[NoAutomaticTrigger]
		public async Task Run(string input, ILogger log)
#endif
		{
			string sb = await GenerateReleaseNotesMarkdown();
			string iterationName = await GetCurrentIterationName();

			using (WikiHttpClient wikiHttpClient = new WikiHttpClient(_uri, credentials))
			{
				try
				{
					WikiPageResponse wikiPage = await wikiHttpClient.GetPageAsync(_project, _wikiName, $"/{iterationName}");
					await wikiHttpClient.CreateOrUpdatePageAsync(new WikiPageCreateOrUpdateParameters()
					{
						Content = sb.ToString()
					}, _project, _wikiName, $"/{iterationName}", wikiPage.ETag.First().ToString(), $"Updated on release notes on {DateTime.UtcNow}(UTC)");
				}
				catch (VssServiceException ex)
				{
					/* 
					 * What an ugly pattern to code with! 
					 * Had to do this since the GetPageAsync call throws exception if page is not found.
					 * Hopefully, I fix this in the future.
					*/
					log.LogInformation("Service exception raised. Assumed that the wiki page does not exist. Trying to create one.");
					await wikiHttpClient.CreateOrUpdatePageAsync(new WikiPageCreateOrUpdateParameters()
					{
						Content = sb.ToString()
					}, _project, _wikiName, $"/{iterationName}", null, $"Added release notes for {iterationName}");
				}
				catch
				{
					throw;
				}
			}
			log.LogInformation("Completed execution. Please check your wiki for your release notes.");
		}

		private async Task<string> GenerateReleaseNotesMarkdown()
		{
			Wiql wiql = new Wiql()
			{
				Query = "SELECT [System.Title],[Custom.Notes] " +
					   "FROM WorkItems " +
					   "WHERE [System.WorkItemType] = 'Product Backlog Item' " +
					   "AND [Custom.ReleaseNotes] = true " +
					   "And [System.TeamProject] = '" + _project + "' " +
					   "AND [System.IterationPath] = @currentIteration('" + _teamName + "')"
			};
			StringBuilder sb = new StringBuilder();
			using (WorkItemTrackingHttpClient workItemTrackingHttpClient = new WorkItemTrackingHttpClient(_uri, credentials))
			{
				WorkItemQueryResult workItemQueryResult = await workItemTrackingHttpClient.QueryByWiqlAsync(wiql);
				//some error handling                
				if (workItemQueryResult.WorkItems.Count() != 0)
				{
					//need to get the list of our work item ids and put them into an array
					List<int> list = new List<int>();
					foreach (var item in workItemQueryResult.WorkItems)
					{
						list.Add(item.Id);
					}
					int[] arr = list.ToArray();

					//build a list of the fields we want to see
					string[] fields = new string[2];
					fields[0] = "System.Title";
					fields[1] = "Custom.Notes";

					//get work items for the ids found in query
					List<WorkItem> workItems = await workItemTrackingHttpClient.GetWorkItemsAsync(arr, fields);

					Converter converter = new Converter(new CustomScheme());

					sb.Append("# Features\n");
					foreach (var workItem in workItems)
					{
						string title = workItem.Fields["System.Title"].ToString();
						string link = Regex.Replace(title, @"\s+", "-");
						sb.Append($"- [{title}](#{link})\n\n\n");
					}

					//loop though work items and write to console
					foreach (var workItem in workItems)
					{
						string title = workItem.Fields["System.Title"].ToString();
						sb.Append($"# {title}\n");
						string md = converter.Convert(workItem.Fields["Custom.Notes"].ToString());
						sb.Append(md);
					}
				}
			}
			return sb.ToString();
		}

		private async Task<string> GetCurrentIterationName()
		{
			using (WorkHttpClient workHttpClient = new WorkHttpClient(_uri, credentials))
			{
				List<TeamSettingsIteration> iteration = await workHttpClient.GetTeamIterationsAsync(new TeamContext(_project), timeframe: "current");
				if (iteration.Count != 1)
				{
					throw new ApplicationException($"There was an error fetching iterations for the project. Found {iteration.Count} iteration. Need exactly 1 iteration.");
				}
				string iterationName = iteration.First().Name;
				return iterationName;
			}
		}
	}
}
