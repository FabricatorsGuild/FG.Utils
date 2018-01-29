using System;
using System.Text;

namespace FG.Utils.BuildTools
{
    public static class PathExtensions
	{

		public static string GetAbsolutePath(string relativePath)
		{
			var currentDirectory = System.IO.Directory.GetCurrentDirectory();
			return GetAbsolutePath(currentDirectory, relativePath);
		}

		public static string GetAbsolutePath(string basePath, string relativePath)
		{
			var combinedPath = relativePath;
			if (!System.IO.Path.IsPathRooted(relativePath))
			{
				combinedPath = System.IO.Path.Combine(basePath, relativePath);
			}

		    var wildCardIndex = combinedPath.IndexOf("*", StringComparison.Ordinal);
		    if (wildCardIndex != -1)
		    {
		        var nonWildCardPart = combinedPath.Substring(0, wildCardIndex);
		        var wildCardPart = combinedPath.Substring(wildCardIndex);

		        var nonWildCardPartPath = System.IO.Path.GetFullPath((new Uri(nonWildCardPart)).LocalPath);

		        return $"{nonWildCardPartPath}{wildCardPart}";
		    }

			return System.IO.Path.GetFullPath((new Uri(combinedPath)).LocalPath);
		}

		public static string GetEvaluatedPath(string path)
		{
			if (!System.IO.Path.IsPathRooted(path))
			{
				return GetAbsolutePath(path);
			}

			return System.IO.Path.GetFullPath((new Uri(path)).LocalPath);
		}

        

	    /// <summary>
	    /// Creates a relative path from one file or folder to another.
	    /// </summary>
	    /// <param name="fromPath">Contains the directory that defines the start of the relative path.</param>
	    /// <param name="toPath">Contains the path that defines the endpoint of the relative path.</param>
	    /// <returns>The relative path from the start directory to the end path or <c>toPath</c> if the paths are not related.</returns>
	    /// <exception cref="ArgumentNullException"></exception>
	    /// <exception cref="UriFormatException"></exception>
	    /// <exception cref="InvalidOperationException"></exception>
		public static string MakeRelativePath(string basePath, string path)
		{
			path = path.Replace(System.IO.Path.AltDirectorySeparatorChar, System.IO.Path.DirectorySeparatorChar);
			basePath = basePath.Replace(System.IO.Path.AltDirectorySeparatorChar, System.IO.Path.DirectorySeparatorChar);
			var pathCommonToBase = path.RemoveCommonPrefix(basePath, System.IO.Path.DirectorySeparatorChar, StringComparison.InvariantCultureIgnoreCase);
			var commonPrefix = path.RemoveFromEnd(pathCommonToBase);
			if (commonPrefix.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString()))
			{
				commonPrefix = commonPrefix.RemoveFromEnd(System.IO.Path.DirectorySeparatorChar.ToString());
			}

			var basePathFromCommonPrefix = basePath.RemoveFromStart(commonPrefix);
			var backTrailingComponents = basePathFromCommonPrefix.Split(new char[] { System.IO.Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

			var builder = new StringBuilder();
			foreach (var backTrailingComponent in backTrailingComponents)
			{
				builder.Append("..");
				builder.Append(System.IO.Path.DirectorySeparatorChar);
			}
			builder.Append(pathCommonToBase);

			return builder.ToString();
		}
	}
}