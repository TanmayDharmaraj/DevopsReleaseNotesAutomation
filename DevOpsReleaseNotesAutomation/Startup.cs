using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(DevOpsReleaseNotesAutomation.Startup))]
namespace DevOpsReleaseNotesAutomation
{
	public class Startup : FunctionsStartup
	{
		public override void Configure(IFunctionsHostBuilder builder)
		{
			builder.Services.AddSingleton<IConfiguration>((serviceProvider) =>
			{
				IConfigurationRoot configuration = new ConfigurationBuilder()
					  .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
					  .AddEnvironmentVariables()
					  .Build();
				return configuration;
			});
		}
	}
}
