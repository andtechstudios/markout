using System;

namespace Andtech.Markout.Console
{
	internal class Macros
	{

		public static string JoinLine(params string[] paths)
		{
			return string.Join(Environment.NewLine, paths) + Environment.NewLine;
		}
	}
}
