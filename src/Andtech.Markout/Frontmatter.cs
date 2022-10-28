using System;
using System.Text.RegularExpressions;

namespace Andtech.Markout
{

	public class Frontmatter
	{
		public bool IsDraft { get; set; }
		public bool IsFuture { get; set; }
		public string RawText { get; set; }

		public static Frontmatter Parse(string text)
		{
			var frontmatter = new Frontmatter()
			{
				RawText = text,
			};

			if (frontmatter.TryGetValue("publishDate", out var publishDate))
			{
				frontmatter.IsFuture = DateTime.Parse(publishDate) > DateTime.Now;
			}

			if (frontmatter.TryGetValue("draft", out var draft))
			{
				frontmatter.IsDraft = bool.Parse(draft);
			}

			return frontmatter;
		}

		public bool TryGetValue(string key, out string value)
		{
			var match = Regex.Match(RawText, $@"{key}:\s*['\""]?(?<value>.*)['\""]?");
			if (match.Success)
			{
				value = match.Groups["value"].Value;
			}
			else
			{
				value = null;
			}

			return match.Success;
		}
	}

}
