using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace FG.Utils.BuildTools
{
	public class NugetTool
	{
		private readonly XDocument _document;

		private readonly string _filePath;
		public string FilePath => _filePath;
		public string FolderPath => System.IO.Path.GetDirectoryName(_filePath);

		public NugetTool(string path)
		{
			_filePath = path;
			_document = XDocument.Load(_filePath);
		}

		public IEnumerable<NugetPackage> GetNugetPackages()
		{
			var packageElements = _document
				.Element(@"packages")?
				.Elements(@"package")
				.Select(e => new NugetPackage
				{
					Name = e.Attribute("id")?.Value ?? "",
					Version = e.Attribute("version")?.Value ?? "",
					TargetFramework = e.Attribute("targetFramework")?.Value ?? "",
				});
			return packageElements;
		}

		public void RemovePackage(string name)
		{
			var element = _document
				.Element(@"packages")?
				.Elements(@"package")
				.FirstOrDefault(e => e.Attribute("id")?.Value == name);

			if (element != null)
			{
				element.Remove();
				_document.Save(_filePath);
			}
		}

		public void AddPackage(string id, string version, string targetFramework)
		{
			var element = _document
				.Element(@"packages")?
				.Elements(@"package")
				.FirstOrDefault(e => e.Attribute("id")?.Value == id);

			if (element == null)
			{
				_document.Element(@"packages").Add(new XElement("package",
					new XAttribute("id", id),
					new XAttribute("version", version),
					new XAttribute("targetFramework", targetFramework)));
				_document.Save(_filePath);
			}
		}
	}
}