using System;
using System.Text;
using System.Text.RegularExpressions;

namespace FG.Utils.BuildTools
{
	public class ReferenceVersion : IComparable
	{
		private readonly string _versionString;

		private static readonly Regex ParseNugetVersionRegex =
			new Regex(@"(\.|^)(?<major>\d+)(?>\.(?<minor>\d+)){0,1}(?>\.(?<rev>\d+)){0,1}(?>\.(?<build>\d+)){0,1}(?>\-(?<prerelease>[^\\]+)){0,1}", RegexOptions.Compiled);
		public ReferenceVersion(string versionString)
		{
			_versionString = versionString;
			var match = ParseNugetVersionRegex.Match(versionString);
			if (!match.Success)
			{
				return;
			}

			var major = match.Groups["major"]?.Value;
			var minor = match.Groups["minor"]?.Value;
			var rev = match.Groups["rev"]?.Value;
			var build = match.Groups["build"]?.Value;
			var prerelease = match.Groups["prerelease"]?.Value;

			if (!string.IsNullOrWhiteSpace(major ))
			{
				Major = int.Parse(major);
			}
			if (!string.IsNullOrWhiteSpace(minor))
			{
				Minor = int.Parse(minor);
			}
			if (!string.IsNullOrWhiteSpace(rev ))
			{
				Revision = int.Parse(rev);
			}
			if (!string.IsNullOrWhiteSpace(build))
			{
				Build = int.Parse(build);
			}
			Prerelease = prerelease;
		}

		public int Major { get; set; }
		public int? Minor { get; set; }
		public int? Revision { get; set; }
		public int? Build { get; set; }
		public string Prerelease { get; set; }
		public int CompareTo(object obj)
		{
			if (obj is ReferenceVersion)
			{
				var b = (ReferenceVersion)obj;

				var major = Major.CompareTo(b.Major);
				if (major != 0) return major;

				var minor = (Minor ?? 0).CompareTo((b.Minor ?? 0));
				if (minor != 0) return minor;

				var revision = (Revision ?? 0).CompareTo((b.Revision ?? 0));
				if (revision != 0) return revision;

				var build = (Build ?? 0).CompareTo((b.Build ?? 0));
				if (build != 0) return build;

				var prerelease = string.Compare(Prerelease, b.Prerelease, StringComparison.InvariantCultureIgnoreCase);
				if (prerelease != 0) return prerelease;

			}
			return 0;
		}

		public int CompareReleaseVersion(ReferenceVersion obj)
		{
			var b = (ReferenceVersion)obj;

			var major = Major.CompareTo(b.Major);
			if (major != 0) return major;

			var minor = (Minor ?? 0).CompareTo((b.Minor ?? 0));
			if (minor != 0) return minor;	

			return 0;
		}

		public override string ToString()
		{
			var builder = new StringBuilder();
			builder.Append(Major);

			if (Minor != null)
			{
				builder.Append(".");
				builder.Append(Minor);

				if (Revision != null)
				{
					builder.Append(".");
					builder.Append(Revision);

					if (Build != null)
					{
						builder.Append(".");
						builder.Append(Build);
					}
				}
			}
			if (!string.IsNullOrEmpty(Prerelease))
			{
				builder.Append("-");
				builder.Append(Prerelease);
			}

			return builder.ToString();
		}
	}
}