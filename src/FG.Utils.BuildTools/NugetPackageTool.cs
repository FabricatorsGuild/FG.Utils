using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace FG.Utils.BuildTools
{
	public class NugetPackageTool
	{
		private readonly Regex _parseNugetVersionRegex =
			new Regex(@"\.(?<major>\d+)(?>\.(?<minor>\d+)){0,1}(?>\.(?<rev>\d+)){0,1}(?>\.(?<build>\d+)){0,1}(?>\-(?<prerelease>.+)){0,1}\n", RegexOptions.Compiled);

		private readonly string _packagesFolder;
		private readonly string _packageName;
		private readonly string _packageVersion;
		private readonly string _packageTargetFramework;
		private readonly string _packageFolderPath;
		private readonly string _packagePath;
		private readonly ReferenceVersion _version;

		private readonly string _packageLibPath;

		public string TargetFrameworkVersion => _packageTargetFramework;

		public NugetPackageTool(string solutionFolder, string packageName, string packageVersion, string packageTargetFramework)
		{
			_packagesFolder = System.IO.Path.Combine(solutionFolder, "packages");
			_packageName = packageName;
			_packageVersion = packageVersion;
			_version = new ReferenceVersion(_packageVersion);
			_packageTargetFramework = packageTargetFramework;

			var packageFolderName = $"{_packageName}.{_packageVersion}";
			_packageFolderPath = System.IO.Path.Combine(_packagesFolder, packageFolderName);
			var packageFileName = $"{_packageName}.{_packageVersion}.nupkg";
			_packagePath = System.IO.Path.Combine(_packageFolderPath, packageFileName);

			_packageLibPath = System.IO.Path.Combine(_packageFolderPath, "lib");
		}

		public static NugetPackageTool GetNugetPackageTool(string solutionFolder, string packageName, string targetFramework)
		{
			var packagesFolder = System.IO.Path.Combine(solutionFolder, "packages");
			
			var directories = System.IO.Directory.GetDirectories(packagesFolder, $"{packageName}*");

			var latestVersionFolder = directories.Select(versionFolder => new ReferenceVersion(System.IO.Path.GetFileName(versionFolder))).OrderByDescending(v => v).FirstOrDefault();

			if (latestVersionFolder != null)
			{
				return new NugetPackageTool(solutionFolder, packageName, latestVersionFolder.ToString(), targetFramework);
			}

			return null;
		}

		public static string[] FindNugetPackages(string packagesFolder, string packageName)
		{
			if( packagesFolder == null) return new string[0];
			var directories = System.IO.Directory.GetDirectories(packagesFolder, $"{packageName}*");
			return directories;
		}

		public Nuspec GetNuspec()
		{
			using (var zipArchive = System.IO.Compression.ZipFile.Open(_packagePath, System.IO.Compression.ZipArchiveMode.Read))
			{
				var nuspecFileName = $"{_packageName}.nuspec";
				var zipArchiveEntry = zipArchive.GetEntry(nuspecFileName);

				var nuspecFileStream = zipArchiveEntry.Open();
			    XDocument document = null;
				document = XDocument.Load(nuspecFileStream);

				var nsNs = XNamespace.Get("http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd");
				var dependencies = document?.Element(nsNs + "package")?.Element(nsNs + "metadata")?.Element(nsNs + "dependencies")?.Elements(nsNs + "dependency")
					.Select(e => new NuspecDependency(){ Name = e.Attribute("id")?.Value, Version = e.Attribute("version")?.Value});
				var nuspec = new Nuspec
				{
					Name = _packageName,
					Dependencies = dependencies?.Where(d => d != null).ToArray() ?? new NuspecDependency[0],
					Version = _version,
				};

				return nuspec;
			}
		}

		public IEnumerable<string> GetLibPaths()
		{
			var libFolderPaths = System.IO.Directory.GetDirectories(_packageLibPath);

			foreach (var libFolderPath in libFolderPaths)
			{
				var libFolderFramework = System.IO.Path.GetFileNameWithoutExtension(libFolderPath);
				if ((_packageTargetFramework != null) && (libFolderFramework == _packageTargetFramework))
				{
					return System.IO.Directory.GetFiles(libFolderPath, "*.dll");
				}
			}
			return new string[0];
		}
	}
}