using Andtech.Common;
using CommandLine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Andtech.Markout.Console
{

	public class Options
	{
		[Option("verbosity", Required = false, HelpText = "Set output verbosity of messages.")]
		public Verbosity Verbosity { get; set; }
		[Option('n', "dry-run", Required = false, HelpText = "Dry run the operation.")]
		public bool DryRun { get; set; }

		[Option("input", Required = false, HelpText = "Input directory.")]
		public string Input { get; set; }
		[Option("output", Required = false, HelpText = "Output directory.")]
		public string Output { get; set; }
	}

	internal class Excerpt
	{
		public string Address { get; set; }
		public string Text { get; set; }
	}

	public class Runner
	{

		static readonly Regex RegexHeading = new Regex(@"^#+\s+(?<value>.+)");

		public void Run(string[] args)
		{
			Parser.Default.ParseArguments<Options>(args)
				.WithParsed(OnParse);

			void OnParse(Options options)
			{
				Log.Verbosity = options.Verbosity;
				DryRun.IsDryRun = options.DryRun;

				var contentRoot = options.Input;
				var outputRoot = string.IsNullOrEmpty(options.Output) ? contentRoot : options.Output;

				if (outputRoot != contentRoot)
				{
					if (Directory.Exists(outputRoot))
					{
						Directory.Delete(outputRoot, true);
					}
				}

				// Begin code
				var excerptDictionary = new Dictionary<Hashtag, List<Excerpt>>();
				var searchDir = contentRoot;
				foreach (var path in Directory.EnumerateFiles(searchDir, "*.md", SearchOption.AllDirectories))
				{
					var post = Post.Read(path);

					if (post.Frontmatter.IsFuture)
					{
						Log.WriteLine($"Skipping future post '{path}'...", ConsoleColor.Yellow);
						continue;
					}
					if (post.Frontmatter.IsDraft)
					{
						Log.WriteLine($"Skipping draft '{path}'...", ConsoleColor.Yellow);
						continue;
					}

					ProcessPost(post);
				}

				GenerateHashtagsIndex();
				GenerateHashtagsPages();

				void ProcessPost(Post post)
				{
					var relativePath = Path.GetRelativePath(contentRoot, post.Path);
					var destPath = Path.Combine(outputRoot, relativePath);

					for (int i = 0; i < post.Content.Count; i++)
					{
						var line = post.Content[i];
						var text = line.Text;
						foreach (var hashtag in line.Hashtags.OrderBy(x => x))
						{
							text += Shortcode.Hashtag(hashtag.Value);
						}
						line.RawText = text;
					}

					Directory.CreateDirectory(Path.GetDirectoryName(destPath));
					File.WriteAllText(destPath, post.ToString());
					Log.WriteLine($"Shortcodes processed to '{destPath}'", ConsoleColor.Cyan);

					// Compute excepts
					var headers = new Dictionary<string, int>();
					string currentHeading = null;
					foreach (var line in post.Content)
					{
						if (line.IsFenced)
						{
							continue;
						}

						// Check for heading
						var match = RegexHeading.Match(line.Text);
						if (match.Success)
						{
							var heading = match.Groups["value"].Value;
							heading = heading.ToLower().Replace(' ', '-');

							if (headers.ContainsKey(heading))
							{
								var count = headers[heading];
								count += 1;
								headers[heading] = count;

								heading += "-" + (count - 1);
							}
							else
							{
								headers.Add(heading, 1);
							}

							currentHeading = heading;
						}

						// Compute excerpt
						var sourcePath = relativePath;
						sourcePath = Path.Combine(
							Path.GetDirectoryName(sourcePath),
							Path.GetFileNameWithoutExtension(sourcePath));

						var excerpt = new Excerpt()
						{
							Address = $"/{sourcePath}",
							Text = string.Empty,
						};
						if (!string.IsNullOrEmpty(currentHeading))
						{
							excerpt.Address = $"{excerpt.Address}#{currentHeading}";
						}

						// Compute excerpt
						excerpt.Text += Regex.Replace(line.Text, @"^(>|\s|\*|(\d+\.))+", string.Empty);
						foreach (var hashtag in line.Hashtags)
						{
							excerpt.Text += " " + Shortcode.Hashtag(hashtag.Value);
						}

						// Enqueue exerpt to all pages
						foreach (var hashtag in line.Hashtags)
						{
							if (!excerptDictionary.ContainsKey(hashtag))
							{
								var list = new List<Excerpt>();
								excerptDictionary.Add(hashtag, list);
							}

							excerptDictionary[hashtag].Add(excerpt);
						}
					}
				}

				void GenerateHashtagsIndex()
				{
					var tokens = new List<string>();
					foreach (var pair in excerptDictionary.OrderBy(x => x.Key))
					{
						var label = $"#{pair.Key} ({pair.Value.Count})";
						var shortcode = Shortcode.Hashtag(pair.Key.Value, label);
						tokens.Add(shortcode);
					}

					var stubPath = Path.Combine(contentRoot, "tags.md");
					var destPath = Path.Combine(outputRoot, "tags.md");

					var content = Macros.JoinLine(
						"<div class=\"hashtags\">",
						string.Join("<br>", tokens),
						"</div>"
					);

					if (File.Exists(stubPath))
					{
						content = Macros.JoinLine(File.ReadAllText(stubPath), content);
					}

					Directory.CreateDirectory(Path.GetDirectoryName(destPath));
					File.WriteAllText(destPath, content);
					Log.WriteLine($"Created hashtags page '{destPath}'", ConsoleColor.Cyan);
				}

				void GenerateHashtagsPages()
				{
					var start = DateTime.Now.Date;
					int offset = 0;

					// Compilation Pages
					foreach (var pair in excerptDictionary.OrderByDescending(x => x.Key))
					{
						var hashtag = pair.Key;
						var excerpts = pair.Value;
						var stubPath = Path.Combine(contentRoot, "tags", hashtag + ".md");
						var destPath = Path.Combine(outputRoot, "tags", hashtag + ".md");

						string content;
						if (File.Exists(stubPath))
						{
							content = File.ReadAllText(stubPath) + Environment.NewLine;
						}
						else
						{
							var fake = DateTime.UnixEpoch
								.AddSeconds(offset)
								.ToString("yyyy-MM-dd HH:mm:ss");
							var iso = start
								.AddSeconds(offset)
								.ToString("yyyy-MM-dd HH:mm:ss");
							offset++;
							var initialText = string.Join("\n",
								"---",
								$"title: '#{hashtag}'",
								$"description: Compilation page of all hashtags",
								$"date: {fake}",
								$"lastmod: {iso}",
								"---",
								Environment.NewLine
							);
							content = initialText + Environment.NewLine;
						}
						foreach (var excerpt in excerpts.OrderBy(SortLine))
						{
							content += $"* [\\[Source\\]]({excerpt.Address}) {excerpt.Text}{Environment.NewLine}";
						}

						string SortLine(Excerpt excerpt)
						{
							return Regex.Replace(excerpt.Text, @"[^\w]", string.Empty);
						}

						Directory.CreateDirectory(Path.GetDirectoryName(destPath));
						File.WriteAllText(destPath, content);
						Log.WriteLine($"Created tag page '{destPath}'", ConsoleColor.Cyan);
					}
				}
			}
		}
	}
}