using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Andtech.Markout
{

	public class Hashtag : IComparable<Hashtag>
	{
		public string Value { get; set; }

		public Hashtag(string value)
		{
			Value = value.Trim('#');
		}

		public override string ToString() => Value;

		public override bool Equals(object obj)
		{
			if (obj is not Hashtag other)
			{
				return false;
			}

			return Value.Equals(other.Value);
		}

		public override int GetHashCode() => Value.GetHashCode();

		public int CompareTo(Hashtag other) => Value.CompareTo(other.Value);
	}

	public class Line
	{
		public bool IsFenced { get; set; }
		public string RawText { get; set; }
		public string Text { get; set; }
		public List<Hashtag> Hashtags { get; set; } = new List<Hashtag>();

		public override string ToString() => RawText;
	}

	public class Post
	{
		public string Path { get; set; }
		public Frontmatter Frontmatter { get; set; }
		public List<Line> Content { get; set; }

		private static readonly Regex RegexHashtag = new Regex(@"#[a-zA-Z0-9-]+");
		private static readonly Regex RegexHashtagSet = new Regex(@"(#[a-zA-Z0-9-]+\s*?)+$", RegexOptions.Multiline);

		public static Post Read(string path)
		{
			var lines = File.ReadAllLines(path);
			int n = lines.Length;

			var frontmatterLines = new List<string>(0);
			var contentLinesRaw = new List<string>(n);

			int firstNonEmptyLine = 0;
			while (firstNonEmptyLine < n)
			{
				if (!string.IsNullOrEmpty(lines[firstNonEmptyLine]))
				{
					break;
				}
				firstNonEmptyLine++;
			}

			int firstFrontmatterLine = firstNonEmptyLine;
			if (firstFrontmatterLine < n)
			{
				if (lines[firstFrontmatterLine] == "---")
				{
					firstFrontmatterLine++;
					do
					{
						if (lines[firstFrontmatterLine] == "---")
						{
							break;
						}

						frontmatterLines.Add(lines[firstFrontmatterLine]);
					}
					while (firstFrontmatterLine++ < n);

					firstFrontmatterLine++;
				}
			}
			int firstContentLine = firstFrontmatterLine;

			int i = firstContentLine;
			while (i < n)
			{
				contentLinesRaw.Add(lines[i++]);
			}

			return new Post()
			{
				Frontmatter = Frontmatter.Parse(string.Join("\n", frontmatterLines)),
				Content = ParseLines(contentLinesRaw),
				Path = path,
			};
		}

		public static List<Line> ParseLines(List<string> linesRaw)
		{
			var lines = new List<Line>();

			// Process main content
			var fences = new Stack<string>();
			for (int i = 0; i < linesRaw.Count; i++)
			{
				var rawLine = linesRaw[i];
				var line = new Line()
				{
					RawText = rawLine,
					Text = rawLine,
				};

				if (IsFence(rawLine))
				{
					if (fences.Count > 0 && rawLine == fences.Peek())
					{
						fences.Pop();
					}
					else
					{
						fences.Push(rawLine);
					}
				}
				else
				{
					var isFenced = fences.Count > 0;
					if (!isFenced)
					{
						line.Text = RegexHashtagSet.Replace(rawLine, ProcessToken);

						string ProcessToken(Match match)
						{
							return RegexHashtag.Replace(match.Value, ReplaceHashtag);

							string ReplaceHashtag(Match match)
							{
								var value = match.Value.Trim('#');
								line.Hashtags.Add(new Hashtag(value));

								return string.Empty;
							}
						}
					}
				}

				line.IsFenced = fences.Count > 0;
				lines.Add(line);
			}

			return lines;
		}

		public override string ToString()
		{
			return string.Join(Environment.NewLine,
				"---",
				Frontmatter.RawText,
				"---",
				string.Join(Environment.NewLine, Content),
				Environment.NewLine
			);
		}

		static bool IsFence(string line)
		{
			return line == "```" || line == "$$";
		}
	}
}
