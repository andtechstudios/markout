namespace Andtech.Markout
{

	public static class Shortcode
	{

		public static string Hashtag(string name, string label = null)
		{
			var openingTag = "{{% hashtag %}}";
			var closingTag = "{{% /hashtag %}}";
			if (!string.IsNullOrEmpty(label))
			{
				openingTag = $"{{{{% hashtag \"{label}\"%}}}}";
			}
			return openingTag + "#" + name + closingTag;
		}
	}
}
