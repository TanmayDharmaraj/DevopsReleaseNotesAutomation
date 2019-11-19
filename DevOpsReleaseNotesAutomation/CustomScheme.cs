using Html2Markdown.Replacement;
using Html2Markdown.Scheme;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DevOpsReleaseNotesAutomation
{
	internal class PatternReplacer : IReplacer
	{

		public string Pattern { get; set; }

		public string Replacement { get; set; }
		public string Replace(string html)
		{
			var regex = new Regex(Pattern);

			return regex.Replace(html, Replacement);
		}
	}
	public class CustomScheme : Markdown, IScheme
	{
		private readonly IList<IReplacer> _replacers;
		public CustomScheme() : base()
		{
			_replacers = base.Replacers();
			_replacers.Add(new PatternReplacer
			{
				Pattern = @"</?div[^>]*>",
				Replacement = ""
			});
			_replacers.Add(new PatternReplacer()
			{
				Pattern = @"</?span[^>]*>",
				Replacement = ""
			});
		}
		public new IList<IReplacer> Replacers()
		{
			return _replacers;
		}
	}
}
