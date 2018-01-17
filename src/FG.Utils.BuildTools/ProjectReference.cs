using System;

namespace FG.Utils.BuildTools
{
	public enum ProjectReferenceType
	{
		GAC,
		DLL,
		NuGet,
		Project
	}

	public class ProjectReference : IComparable
	{
		public string Source { get; set; }
		public ProjectReferenceType ReferenceType { get; set; }
		public string Name { get; set; }
		public string FullName { get; set; }
		public string HintPath { get; set; }
		public ReferenceVersion Version { get; set; }
		public string PackageName { get; set; }
		public ReferenceVersion PackageVersion { get; set; }
		public bool Private { get; set; }
		public bool IsPackageReference { get; set; }


		public override string ToString()
		{
			return $"Project reference: {Name} {Version} {HintPath}";
		}

		public int CompareTo(object other)
		{
			if(!( other is ProjectReference))
			{
				return 0;
			}

			var b = (ProjectReference) other;

			var nameComparison = string.Compare(Name, b.Name, StringComparison.InvariantCultureIgnoreCase);
			if (nameComparison != 0) return nameComparison;

			var versionComparison = Version.CompareTo(b.Version);
			if (versionComparison != 0) return versionComparison;

			if (IsPackageReference)
			{
				var packageVersionComparison = PackageVersion.CompareTo(b.PackageVersion);
				if (packageVersionComparison != 0) return packageVersionComparison;
			}

			return string.Compare(FullName, b.FullName, StringComparison.Ordinal);
		}
	}
}