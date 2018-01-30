using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace FG.Utils.BuildTools
{
	public class ProjectTool
	{
		private readonly Regex _parseTargetFrameworkVersion = new Regex(@"v(?<a>\d+)(?>\.(?<b>\d+)(?>\.(?<c>\d+)(?>\.(?<d>\d+)){0,1}){0,1}){0,1}", RegexOptions.Compiled);
		private readonly string _parseTargetFrameworkReplacementString = @"net$1$2$3$4";
		private static readonly Regex _isPackagesHintPath = new Regex(@"\\packages\\.*\\lib\\.*");
		private static readonly Regex _packageNameFromHintPath = new Regex(@"\\packages\\(?<package>[^\\]*)");
        private readonly Regex _parseProjectCondition = new Regex(@"\'(?<left>[^']*)\'\s*(?<operator>==|!=)\s*\'(?<right>[^']*)\'", RegexOptions.Compiled);
	    private readonly Regex _replaceProjectVarablesInCondition = new Regex(@"(!?\$\((?<varable>[^\)]*)\))", RegexOptions.Compiled);

		private readonly string _projectPath;
		private readonly ILogger _logger;
		private readonly XDocument _document;
		private readonly XmlNamespaceManager _namespaceManager;
		private readonly XNamespace _msbNs;
		private const string MsbuildXmlNamespace = @"http://schemas.microsoft.com/developer/msbuild/2003";

		private readonly bool _isCpsDocument = false;

		public bool IsCpsDocument => _isCpsDocument;

		private bool _didUpdateDocument = false;
		private string _originalHash;

		public string FilePath => _projectPath;
		public string FolderPath => System.IO.Path.GetDirectoryName(_projectPath);
		public string Name => System.IO.Path.GetFileNameWithoutExtension(_projectPath);

		public ProjectTool(string path, ILogger logger)
		{
			_projectPath = path;
			_logger = logger;

			_document = XDocument.Load(_projectPath);

			var xws = new XmlWriterSettings
			{
				OmitXmlDeclaration = _isCpsDocument,
				Indent = true
			};
			using (var stream = new MemoryStream())
			{
				using (var writer = XmlWriter.Create(stream, xws))
				{
					_document.Save(writer);
				}
				stream.Position = 0;
				using (var sha = SHA256.Create())
				{
					var computedHash = sha.ComputeHash(stream);
					_originalHash = Encoding.UTF8.GetString(computedHash);
				}
			}

			var sdkValue = (_document.FirstNode as XElement)?.Attribute("Sdk")?.Value;
			_isCpsDocument = (sdkValue != null);
			if (_isCpsDocument)
			{
				_msbNs = "";
			}
			else
			{
				_msbNs = MsbuildXmlNamespace;
			}



			//_namespaceManager = new XmlNamespaceManager(_document.NameTable);
			//_namespaceManager.AddNamespace("msbld", MsbuildXmlNamespace);

			// //ItemGroup/ProjectReference

			// //ItemGroup/Reference

		}

		public IEnumerable<ProjectReference> GetReferences()
		{
			var itemGroupReferences = _document.Descendants(_msbNs + "ItemGroup")
				.SelectMany(element => element.Elements(_msbNs + "Reference"));

			var projectTargetVersion = (_document.Descendants(_msbNs + "TargetFrameworkVersion").FirstOrDefault()?.Value ?? "v4.5.1").RemoveFromStart("v");

			foreach (var element in itemGroupReferences)
			{
				
				var fullName = element.Attribute("Include")?.Value;
				var hintPath = element.Attribute("HintPath")?.Value;
				var nameComponents = fullName.Split(',');
				var name = nameComponents[0];
				var version = nameComponents.Length > 1 ? nameComponents[1]?.RemoveFromStart(" Version=") : projectTargetVersion;
				var projectReference = new ProjectReference()
				{
					Name = name, 
					FullName = fullName,
					HintPath = hintPath,
					Version = new ReferenceVersion(version),
				};

				yield return projectReference;
			}
		}

		public IEnumerable<NugetPackage> GetPackageReferences()
		{
			if (_isCpsDocument)
			{

				var packageReferences = _document.Descendants(_msbNs + "ItemGroup")
					.SelectMany(element => element.Elements(_msbNs + "PackageReference"));

				return packageReferences.Select(element =>
				{
					var name = element.Attribute("Include")?.Value;
					var version = element.Attribute("Version")?.Value;
					var projectReference = new NugetPackage()
					{
						Name = name,
						Version = version,
					};

					return projectReference;

				}).ToArray();				
			}
			else
			{
				var nugetPackagesConfigPath = PathExtensions.GetAbsolutePath(FolderPath, "packages.config");

				if (System.IO.File.Exists(nugetPackagesConfigPath))
				{

					var nugetTool = new NugetTool(nugetPackagesConfigPath);

					return nugetTool.GetNugetPackages();
				}
			}

			return new NugetPackage[0];
		}

		public void Save()
		{
			if (!_didUpdateDocument) return;

			var xws = new XmlWriterSettings
			{
				OmitXmlDeclaration = _isCpsDocument,
				Indent = true
			};

			using (var stream = new MemoryStream())
			{
				using (var writer = XmlWriter.Create(stream, xws))
				{
					_document.Save(writer);
				}
				stream.Position = 0;
				using (var sha = SHA256.Create())
				{
					var computedHash = sha.ComputeHash(stream);
					var newHash = Encoding.UTF8.GetString(computedHash);

					if( newHash == _originalHash) return;					
				}
			}

			
			using (var writer = XmlWriter.Create(_projectPath, xws))
			{
				_document.Save(writer);
			}
		}

		public void AddProjectReference(string name, string path, Guid projectId)
		{
			if (_isCpsDocument)
			{
				var itemGroups = _document.Element("Project")?.Elements(_msbNs + "ItemGroup")
					.SelectMany(element => element.Elements(_msbNs + "ProjectReference"));
				XElement parentNode = null;
				if (itemGroups.Any())
				{
					var existingProjectReference = itemGroups.First();
					parentNode = existingProjectReference.Parent;
				}
				else
				{
					var itemGroupElement = new XElement(_msbNs + "ItemGroup");
					_document.Element(_msbNs + "Project").Add(itemGroupElement);
					parentNode = itemGroupElement;
				}

				var existingProjectReferenceXElement = _document
					.Element("Project")?
					.Elements("ItemGroup")
					.SelectMany(e => e.Elements("ProjectReference"))
					.FirstOrDefault(e => e?.Attribute("Include")?.Value == path);
				if (existingProjectReferenceXElement == null)
				{
					var projectReferenceXElement = new XElement(_msbNs + "ProjectReference",
						new XAttribute("Include", path)
					);
					parentNode.Add(projectReferenceXElement);

					_didUpdateDocument = true;
				}

			}
			else
			{
				var itemGroups = _document.Descendants(_msbNs + "ItemGroup")
					.SelectMany(element => element.Elements(_msbNs + "ProjectReference"));
				XElement parentNode = null;
				if (itemGroups.Any())
				{
					var existingProjectReference = itemGroups.First();
					parentNode = existingProjectReference.Parent;
				}
				else
				{
					var itemGroupElement = new XElement(_msbNs + "ItemGroup");
					_document.Element(_msbNs + "Project").Add(itemGroupElement);
					parentNode = itemGroupElement;
				}

				var existingProjectReferenceXElement = _document
					.Elements("ItemGroup")
					.SelectMany(e => e.Elements("ProjectReference"))
					.Select(e => e.Element("Project"))
					.FirstOrDefault(e => e?.Value == name);
				if (existingProjectReferenceXElement == null)
				{
					var projectReferenceXElement = new XElement(_msbNs + "ProjectReference",
						new XAttribute("Include", path),
						new XElement(_msbNs + "Project", $"{{{projectId}}}"),
						new XElement(_msbNs + "Name", name)
					);
					parentNode.Add(projectReferenceXElement);

					_didUpdateDocument = true;
				}
			}
		}

		public void RemoveReference(string name)
		{
			var referenceElement = _document.Descendants(_msbNs + "ItemGroup")
				.SelectMany(element => element.Elements(_msbNs + "Reference"))
				.FirstOrDefault(element => element.Attribute("Include")?.Value.Split(',').FirstOrDefault() == name);

			referenceElement?.Remove();

			_didUpdateDocument = true;
		}

		public void RemovePackageReference(string name)
		{
			if (_isCpsDocument)
			{
				var referenceElement = _document.Descendants(_msbNs + "ItemGroup")
					.SelectMany(element => element.Elements(_msbNs + "PackageReference"))
					.FirstOrDefault(element => element.Attribute("Include")?.Value == name);

				referenceElement?.Remove();

				_didUpdateDocument = true;
			}
			else
			{
				var nugetPackagesConfigPath = PathExtensions.GetAbsolutePath(FolderPath, "packages.config");

				if (System.IO.File.Exists(nugetPackagesConfigPath))
				{

					var nugetTool = new NugetTool(nugetPackagesConfigPath);
					nugetTool.RemovePackage(name);
				}
			}
		}

		public void AddNugetReference(string packageName, string packageVersion, string targetVersion)
		{
			if (_isCpsDocument)
			{
				var projectElement = _document.Element("Project");
				if (projectElement == null)
					throw new NotSupportedException("Expected project file to have a <Project... /> element");

				var packageReferenceElements = projectElement.Elements(_msbNs + "ItemGroup")
					.SelectMany(element => element.Elements(_msbNs + "PackageReference"));
				XElement parentNode = null;
				if (packageReferenceElements.Any())
				{
					var existingPackageReference = packageReferenceElements.First();
					parentNode = existingPackageReference.Parent;
				}
				else
				{
					var itemGroupElement = new XElement(_msbNs + "ItemGroup");
					projectElement.Add(itemGroupElement);
					parentNode = itemGroupElement;
				}

				var existingProjectReferenceXElement = projectElement
					.Elements("ItemGroup")
					.SelectMany(e => e.Elements("ProjectReference"))
					.FirstOrDefault(e => e?.Attribute("Include")?.Value == packageName);
				if (existingProjectReferenceXElement == null)
				{
					var projectReferenceXElement = new XElement(_msbNs + "PackageReference",
						new XAttribute("Include", packageName),
						new XAttribute("Version", packageVersion)
					);
					parentNode.Add(projectReferenceXElement);

					_didUpdateDocument = true;
				}
				else
				{
					var versionAttribute = existingProjectReferenceXElement.Attribute("Version");

					if (versionAttribute == null)
					{
						existingProjectReferenceXElement.Add(new XAttribute("Version", packageVersion));
					}
					else if (versionAttribute.Value != packageVersion)
					{
						versionAttribute.Value = packageVersion;
					}
				}
			}
			else
			{
				var nugetPackagesConfigPath = PathExtensions.GetAbsolutePath(FolderPath, "packages.config");

				if (System.IO.File.Exists(nugetPackagesConfigPath))
				{

					var nugetTool = new NugetTool(nugetPackagesConfigPath);
					nugetTool.AddPackage(packageName, packageVersion, targetVersion);
				}
			}
		}

		public IEnumerable<ProjectReference> GetProjectReferences()
		{
			if (_isCpsDocument)
			{
				var projectElement = _document.Element("Project");
				if (projectElement == null)
					throw new NotSupportedException("Project document is missing the Project element");

				var projectReferences = projectElement.Elements(_msbNs + "ItemGroup")
					.SelectMany(element => element.Elements(_msbNs + "ProjectReference"))
					.Select(e => new ProjectReference()
					{
						Name = System.IO.Path.GetFileNameWithoutExtension(e?.Attribute("Include")?.Value),
						HintPath = e?.Attribute("Include")?.Value,
					});
				return projectReferences;
			}
			else
			{
				var projectReferences = _document.Descendants(_msbNs + "ItemGroup")
					.SelectMany(element => element.Elements(_msbNs + "ProjectReference"))
					.Select(e => new ProjectReference()
					{						
						Name = e?.Element(_msbNs + "Name")?.Value,
						HintPath = e?.Attribute("Include")?.Value,
					});
				return projectReferences;
			}
		}

		public void RemoveProjectReference(ProjectReference projectReference)
		{
			var referenceElement = _document.Descendants(_msbNs + "ItemGroup")
				.SelectMany(element => element.Elements(_msbNs + "ProjectReference"))
				.FirstOrDefault(element => element.Attribute("Include")?.Value == projectReference.HintPath);

			referenceElement?.Remove();

			_didUpdateDocument = true;
		}

		public string GetTargetFramework()
		{
			var targetFrameworkElement = "net45";
			if (_isCpsDocument)
			{
				targetFrameworkElement = _document.Element(_msbNs + "Project")?
					                         .Elements(_msbNs + "PropertyGroup")
					                         .Elements(_msbNs + "TargetFrameworkVersion")
					                         .FirstOrDefault()?.Value ?? targetFrameworkElement;
				
			}
			else
			{
				targetFrameworkElement = _document.Element(_msbNs + "Project")?
					                         .Elements(_msbNs + "PropertyGroup")
					                         .Elements(_msbNs + "TargetFrameworkVersion")
					                         .FirstOrDefault()?.Value ?? targetFrameworkElement;
			}

			targetFrameworkElement = _parseTargetFrameworkVersion.Replace(targetFrameworkElement, _parseTargetFrameworkReplacementString);
			return targetFrameworkElement;
		}

		public void AddReference(string referencePath)
		{
			if (!_isCpsDocument)
			{
				var referenceInfo = Assembly.ReflectionOnlyLoadFrom(referencePath);
				var include = $"{referenceInfo.GetName().ToString()}, processorArchitecture={referenceInfo.GetName().ProcessorArchitecture.ToString().ToUpper()}";
				var hintPath = PathExtensions.MakeRelativePath(FolderPath, referencePath);

				var referenceElements = _document.Descendants(_msbNs + "ItemGroup")
					.SelectMany(element => element.Elements(_msbNs + "Reference"));
				XElement parentNode = null;
				if (referenceElements.Any())
				{
					var existingProjectReference = referenceElements.First();
					parentNode = existingProjectReference.Parent;
				}
				else
				{
					var itemGroupElement = new XElement(_msbNs + "ItemGroup");
					_document.Element(_msbNs + "Project").Add(itemGroupElement);
					parentNode = itemGroupElement;
				}

				var existingReferenceXElement = _document
					.Elements("ItemGroup")
					.SelectMany(e => e.Elements("Reference"))
					.FirstOrDefault(e => e.Attribute("Include")?.Value == include);
				if (existingReferenceXElement == null)
				{
					var projectReferenceXElement = new XElement(_msbNs + "Reference",
						new XAttribute("Include", include),
						new XElement(_msbNs + "HintPath", hintPath)
					);
					parentNode.Add(projectReferenceXElement);

					_didUpdateDocument = true;
				}
			}
		}


		public void CleanUpProject()
		{
			if (!_isCpsDocument)
			{
				CleanUpClassicProject();
			}
			else
			{
				CleanUpCPSProject();
			}
		}

		private void CleanUpCPSProject()
		{

		}

		private static bool IsHintPathPackageReference(string hintPath)
		{
			if (hintPath == null) return false;
			var match = _isPackagesHintPath.Match(hintPath);
			return match.Success;
		}

		private static string GetPackageNameFromHintPath(string hintPath)
		{
			var packageName = _packageNameFromHintPath.Match(hintPath)?.Groups["package"]?.Value;
			return packageName;
		}

		private string GetPackagesFolderPath()
		{
			var packagesFolderPath = (string)null;
			var folder = _projectPath;
			while (packagesFolderPath == null)
			{
				folder = System.IO.Directory.GetParent(folder)?.FullName;
				if (folder == null) return null;
				var packagesFolders = System.IO.Directory.GetDirectories(folder, "packages");
				packagesFolderPath = packagesFolders.FirstOrDefault();
			}
			return packagesFolderPath;
		}

		private void CleanUpClassicProject()
		{
			var projectNode = _document.Element(_msbNs + "Project");
			if (projectNode == null) throw new ApplicationException($"Invalid Project xml, could not find Project node");

			var projectTargetVersion = (projectNode.Elements(_msbNs + "TargetFrameworkVersion").FirstOrDefault()?.Value ?? "v4.5.1").RemoveFromStart("v");

			var itemGroupReferenceNodes = projectNode.Elements(_msbNs + "ItemGroup")
				.SelectMany(element => element.Elements(_msbNs + "Reference"))
				.ToArray();

			var references = new List<ProjectReference>();// GetReferences().OrderBy(r => r).ToArray();
			foreach (var referenceNode in itemGroupReferenceNodes)
			{
				var fullName = referenceNode.Attribute("Include")?.Value;
				var hintPath = referenceNode.Element(_msbNs + "HintPath")?.Value;
				var nameComponents = fullName.Split(',');
				var name = nameComponents[0];
				var version = nameComponents.Length > 1 ? nameComponents[1]?.RemoveFromStart(" Version=") : projectTargetVersion;
				var isPrivate = referenceNode.Element(_msbNs + "Private")?.Value;
				var isPackageReference = IsHintPathPackageReference(hintPath);
				var packageName = isPackageReference ? GetPackageNameFromHintPath(hintPath) : null;

				var projectReference = new ProjectReference()
				{
					Name = name,
					FullName = fullName,
					HintPath = hintPath,
					Version = new ReferenceVersion(version),
					Private = (isPrivate == "True"),
					IsPackageReference = isPackageReference,
					PackageName = packageName,
					PackageVersion = isPackageReference ? new ReferenceVersion(hintPath) : null,
				};

				var existingReference = references.FirstOrDefault(r => r.Name == projectReference.Name);
				if (existingReference != null)
				{
					var comparison = projectReference.CompareTo(existingReference);
					if (comparison == 0)
					{
						// Same, ignore
						_logger.LogMessage(
							$"\tFound duplicate identical references of {existingReference.Name} {existingReference.Version} {existingReference.HintPath}");
					}
					else if (comparison > 0)
					{
						// Newer version, remove the existing
						_logger.LogMessage(
							$"\tFound duplicate references of {existingReference.Name} {existingReference.Version} {existingReference.HintPath}");
						references.Remove(existingReference);
						references.Add(projectReference);
					}
					else
					{
						// Newer version, dont add this
						_logger.LogMessage(
							$"\tFound duplicate references of {existingReference.Name} {existingReference.Version} {existingReference.HintPath}");
					}
				}
				else
				{
					references.Add(projectReference);
				}

				if (projectReference.IsPackageReference)
				{
					if (projectReference.PackageVersion.CompareReleaseVersion(projectReference.Version) != 0)
					{
						var packagesFolder = GetPackagesFolderPath();
						var possibleNugetPackagePaths = NugetPackageTool.FindNugetPackages(packagesFolder, projectReference.Name);

						if (possibleNugetPackagePaths.All(packagePath => !hintPath.StartsWith(PathExtensions.MakeRelativePath(FolderPath, packagePath))))
						{
							_logger.LogMessage(
								$"\tFound suspicious hintpath for {projectReference.Name} {projectReference.Version} {projectReference.HintPath}");
						}
					}
				}

				referenceNode.Remove();
			}


			var gacReferencesItemGroupElement = new XElement(_msbNs + "ItemGroup");
			XElement insertAfterElement = null;
			insertAfterElement = projectNode.Elements(_msbNs + "PropertyGroup").LastOrDefault();

			if (insertAfterElement == null)
			{
				projectNode.Add(gacReferencesItemGroupElement);
			}
			else
			{
				insertAfterElement.AddAfterSelf(gacReferencesItemGroupElement);
			}

			references.Sort((a, b) => a.CompareTo(b));

			foreach (var reference in references.Where(r => r.HintPath == null))
			{
				var children = new List<object>() { new XAttribute("Include", reference.FullName) };
				if (reference.HintPath != null) { children.Add(new XElement(_msbNs + "HintPath", reference.HintPath)); }
				if (reference.Private) { children.Add(new XElement(_msbNs + "Private", "True")); }
				var projectReferenceXElement = new XElement(_msbNs + "Reference", children);
				gacReferencesItemGroupElement.Add(projectReferenceXElement);
			}

			insertAfterElement = gacReferencesItemGroupElement;


			var referencesItemGroupElement = new XElement(_msbNs + "ItemGroup");
			insertAfterElement.AddAfterSelf(referencesItemGroupElement);

			references.Sort((a, b) => a.CompareTo(b));

			foreach (var reference in references.Where(r => r.HintPath != null))
			{
				var children = new List<object>() {new XAttribute("Include", reference.FullName)};
				if (reference.HintPath != null) { children.Add(new XElement(_msbNs + "HintPath", reference.HintPath)); }
				if (reference.Private) { children.Add(new XElement(_msbNs + "Private", "True")); }
				var projectReferenceXElement = new XElement(_msbNs + "Reference", children );
				referencesItemGroupElement.Add(projectReferenceXElement);
			}



			// Sort ProjectReferences
			var projectReferenceChildNodes = projectNode.Elements(_msbNs + "ItemGroup").Elements()
				.Where(element => element.Name.LocalName == "ProjectReference")
				.Select(element => new { Element = element, File = element.Attribute("Include")?.Value })
				.Where(element => element.File != null)
				.ToArray();

			foreach (var itemGroupChildNode in projectReferenceChildNodes)
			{
				itemGroupChildNode.Element.Remove();
			}

			var projectReferenceItemGroupElement = new XElement(_msbNs + "ItemGroup");
			referencesItemGroupElement.AddBeforeSelf(projectReferenceItemGroupElement);

			var lastItemFile = "";
			foreach (var itemGroupChildNode in projectReferenceChildNodes.OrderBy(element => element.File))
			{
				if (lastItemFile != itemGroupChildNode.File)
				{
					projectReferenceItemGroupElement.Add(itemGroupChildNode.Element);
				}
				lastItemFile = itemGroupChildNode.File;
			}



			// Sort Includes
			var itemGroupChildNodes = projectNode.Elements(_msbNs + "ItemGroup").Elements()
				.Where(element => (element.Name.LocalName != "Reference") && (element.Name.LocalName != "ProjectReference"))
				.Select(element => new { Element = element, File = element.Attribute("Include")?.Value})
				.Where(element => element.File != null)
				.ToArray();

			foreach (var itemGroupChildNode in itemGroupChildNodes)
			{
				itemGroupChildNode.Element.Remove();
			}

			var includesItemGroupElement = new XElement(_msbNs + "ItemGroup");
			referencesItemGroupElement.AddAfterSelf(includesItemGroupElement);

			foreach (var itemGroupChildNode in itemGroupChildNodes.OrderBy(element => element.File))
			{
				includesItemGroupElement.Add(itemGroupChildNode.Element);
			}


			// Remove empty ItemGroup nodes
			var itemGroupNodes = _document.Descendants(_msbNs + "ItemGroup").ToArray();
			foreach (var itemGroupNode in itemGroupNodes)
			{
				if (!itemGroupNode.HasElements)
				{
					itemGroupNode.Remove();
				}
			}
			_didUpdateDocument = true;
		}

		public IEnumerable<ProjectReference> ScanReferences()
		{
			if (!_isCpsDocument)
			{
				return ScanClassicReferences();
			}
			else
			{
				return ScanCPSReferences();
			}
		}

		private IEnumerable<ProjectReference> ScanClassicReferences()
		{
			var projectNode = _document.Element(_msbNs + "Project");
			if (projectNode == null) throw new ApplicationException($"Invalid Project xml, could not find Project node");

			var projectTargetVersion = (projectNode.Elements(_msbNs + "TargetFrameworkVersion").FirstOrDefault()?.Value ?? "v4.5.1").RemoveFromStart("v");

			var itemGroupReferenceNodes = projectNode.Elements(_msbNs + "ItemGroup")
				.SelectMany(element => element.Elements(_msbNs + "Reference"))
				.ToArray();

			var references = new List<ProjectReference>();// GetReferences().OrderBy(r => r).ToArray();
			foreach (var referenceNode in itemGroupReferenceNodes)
			{
				var fullName = referenceNode.Attribute("Include")?.Value;
				var hintPath = referenceNode.Element(_msbNs + "HintPath")?.Value;
				var nameComponents = fullName.Split(',');
				var name = nameComponents[0];
				var version = nameComponents.Length > 1 ? nameComponents[1]?.RemoveFromStart(" Version=") : projectTargetVersion;
				var isPrivate = referenceNode.Element(_msbNs + "Private")?.Value;
				var isPackageReference = IsHintPathPackageReference(hintPath);
				var packageName = isPackageReference ? GetPackageNameFromHintPath(hintPath) : null;

				var projectReference = new ProjectReference()
				{
					Source = this.Name,
					ReferenceType = isPackageReference ? ProjectReferenceType.NuGet : ( hintPath != null ? ProjectReferenceType.DLL : ProjectReferenceType.GAC),
					Name = name,
					FullName = fullName,
					HintPath = hintPath,
					Version = new ReferenceVersion(version),
					Private = (isPrivate == "True"),
					IsPackageReference = isPackageReference,
					PackageName =  packageName,
					PackageVersion = isPackageReference ? new ReferenceVersion(hintPath) : null,
				};


				references.Add(projectReference);
			}			

			// Sort ProjectReferences
			var projectReferenceChildNodes = projectNode.Elements(_msbNs + "ItemGroup").Elements()
				.Where(element => element.Name.LocalName == "ProjectReference")
				.Select(element => new { Element = element, File = element.Attribute("Include")?.Value })
				.Where(element => element.File != null)
				.ToArray();

			foreach (var itemGroupChildNode in projectReferenceChildNodes.OrderBy(element => element.File))
			{
				var fullName = itemGroupChildNode.Element.Attribute("Include")?.Value;
				var name = System.IO.Path.GetFileNameWithoutExtension(fullName);
				var projectReference = new ProjectReference()
				{
					Source = this.Name,
					ReferenceType = ProjectReferenceType.Project,
					Name = name,
					FullName = fullName,
					HintPath = null,
					Version = null,
					Private = false,
					IsPackageReference = false,
					PackageVersion = null,
				};

				references.Add(projectReference);
			}

			return references.ToArray();
		}

		private IEnumerable<ProjectReference> ScanCPSReferences()
		{
			var projectNode = _document.Element("Project");
			if (projectNode == null) throw new ApplicationException($"Invalid Project xml, could not find Project node");

			var projectTargetVersion = (projectNode.Elements("ProjectGroup").Elements("TargetFramework").FirstOrDefault()?.Value ?? "net461");

			var itemGroupReferenceNodes = projectNode.Elements("ItemGroup").Elements("PackageReference")
				.ToArray();

			var references = new List<ProjectReference>();// GetReferences().OrderBy(r => r).ToArray();
			foreach (var referenceNode in itemGroupReferenceNodes)
			{
				var fullName = referenceNode.Attribute("Include")?.Value;
				var version = referenceNode.Attribute("Version")?.Value;

				var projectReference = new ProjectReference()
				{
					Source = this.Name,
					ReferenceType = ProjectReferenceType.NuGet,
					Name = fullName,
					FullName = fullName,
					HintPath = null,
					Version = new ReferenceVersion(version),
					Private = false,
					IsPackageReference = true,
					PackageVersion = new ReferenceVersion(version),
				};

				references.Add(projectReference);
			}

			// Sort ProjectReferences
			var projectReferenceChildNodes = projectNode.Elements(_msbNs + "ItemGroup").Elements()
				.Where(element => element.Name.LocalName == "ProjectReference")
				.Select(element => new { Element = element, File = element.Attribute("Include")?.Value })
				.Where(element => element.File != null)
				.ToArray();

			foreach (var itemGroupChildNode in projectReferenceChildNodes.OrderBy(element => element.File))
			{
				var fullName = itemGroupChildNode.Element.Attribute("Include")?.Value;
				var name = System.IO.Path.GetFileNameWithoutExtension(fullName);
				var projectReference = new ProjectReference()
				{
					Source = this.Name,
					ReferenceType = ProjectReferenceType.Project,
					Name = name,
					FullName = fullName,
					HintPath = null,
					Version = null,
					Private = false,
					IsPackageReference = false,
					PackageVersion = null,
				};

				references.Add(projectReference);
			}

			return references.ToArray();
		}

		public IEnumerable<FileReference> ScanFilesInProjectFolder()
		{
			if (!_isCpsDocument)
			{
				return ScanFilesInClassicProjectFolder();
			}
			else
			{
				return ScanFilesInCPSProjectFolder();
			}
		}

		private IEnumerable<FileReference> ScanFilesInClassicProjectFolder()
		{
			var projectNode = _document.Element(_msbNs + "Project");
			if (projectNode == null) throw new ApplicationException($"Invalid Project xml, could not find Project node");

			var projectTargetVersion = (projectNode.Elements(_msbNs + "TargetFrameworkVersion").FirstOrDefault()?.Value ?? "v4.5.1").RemoveFromStart("v");

			var discoveredFiles = new Dictionary<string, FileReference>();
			var filesInSubFolders = System.IO.Directory.GetFiles(this.FolderPath, "*", SearchOption.AllDirectories);
			foreach (var fileInSubFolders in filesInSubFolders)
			{
				var path = fileInSubFolders;
				var include = PathExtensions.MakeRelativePath(this.FolderPath, path);
				var fileType = System.IO.Path.GetExtension(include);
				
				var isOutputFolder = include.StartsWith("bin") || include.StartsWith("obj");
				var isTemporaryFile = fileType == ".user";
				var isProjectFile = (fileType == ".csproj");

				if (!isOutputFolder && !isProjectFile && !isTemporaryFile)
				{
					var fileReference = new FileReference()
					{
						IncludeType = "Unknown",
						Name = include,
						Path = path,
						OnDisk = true,
						InProjectFile = false,
					};
					discoveredFiles.Add(path.ToLowerInvariant(), fileReference);
				}	
			}

			var itemGroupIncludeNodes = projectNode.Elements(_msbNs + "ItemGroup")
				.SelectMany(element => element.Elements())
				.Where(element => element.Attribute("Include")?.Value != null)
				.Where(element => element.Name.LocalName != "Reference" && element.Name.LocalName != "ProjectReference")
				.ToArray();

			foreach (var itemGroupIncludeNode in itemGroupIncludeNodes)
			{
				var include = itemGroupIncludeNode.Attribute("Include")?.Value;
				var includeType = itemGroupIncludeNode.Name.LocalName;

				var isPureProjectItem = (new[] {"WCFMetadata", "Folder", "Service", "BootstrapperPackage" }).Contains(includeType);

			    var itemProperties = itemGroupIncludeNode.HasElements ?
			        itemGroupIncludeNode.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value) :
			        new Dictionary<string, string>();

                if (!isPureProjectItem)
				{
					var path = PathExtensions.GetAbsolutePath(this.FolderPath, include);
					var fileReference = new FileReference()
					{
						IncludeType = includeType,
						Path = path,
						Name = include,
						InProjectFile = true,
                        Properties = itemProperties
					};

					fileReference.OnDisk = discoveredFiles.ContainsKey(path.ToLowerInvariant());
					discoveredFiles[path.ToLowerInvariant()] = fileReference;
				}
			}

			return discoveredFiles.Values.ToArray();
		}

		private IEnumerable<FileReference> ScanFilesInCPSProjectFolder()
		{
            // Scan all default included files
		    var discoveredFiles = new Dictionary<string, FileReference>();
		    var filesInSubFolders = System.IO.Directory.GetFiles(this.FolderPath, "*", SearchOption.AllDirectories);

		    foreach (var fileInSubFolders in filesInSubFolders)
		    {
		        var path = fileInSubFolders;
		        var include = PathExtensions.MakeRelativePath(this.FolderPath, path);
		        var fileType = System.IO.Path.GetExtension(include);

		        var isOutputFolder = include.StartsWith("bin") || include.StartsWith("obj");
		        var isTemporaryFile = fileType == ".user";
		        var isProjectFile = (fileType == ".csproj");

                var isCompileFile = (fileType == ".cs");
		        var isNoneFile = (fileType == ".json" || fileType == ".config");

                if (!isOutputFolder && !isProjectFile && !isTemporaryFile)
		        {
		            var fileReference = new FileReference()
		            {
		                IncludeType = isCompileFile ? "Compile" : isNoneFile ? "None" : "Unknown",
		                Name = include,
		                Path = path,
		                OnDisk = true,
		                InProjectFile = false,
		            };
		            discoveredFiles.Add(path.ToLowerInvariant(), fileReference);
		        }
		    }

		    var projectNode = _document.Element("Project");
		    if (projectNode == null) throw new ApplicationException($"Invalid Project xml, could not find Project node");

            // Update explicitly included files
            var itemGroupUpdateNodes = projectNode.Elements("ItemGroup")
		        .SelectMany(element => element.Elements())
		        .Where(element => element.Attribute("Update")?.Value != null)
		        .Where(element => element.Name.LocalName != "Reference" && element.Name.LocalName != "ProjectReference")
		        .ToArray();

		    foreach (var itemGroupUpdateNode in itemGroupUpdateNodes)
		    {
		        var updateName = itemGroupUpdateNode.Attribute("Update")?.Value;
		        var updateType = itemGroupUpdateNode.Name.LocalName;

		        var isPureProjectItem = (new[] { "WCFMetadata", "Folder", "Service", "BootstrapperPackage" }).Contains(updateType);

		        var itemProperties = itemGroupUpdateNode.HasElements ? 
                    itemGroupUpdateNode.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value) : 
                    new Dictionary<string, string>();

                if (!isPureProjectItem)
		        {
		            var path = PathExtensions.GetAbsolutePath(this.FolderPath, updateName);
		            var fileReference = new FileReference()
		            {
		                IncludeType = updateType,
		                Path = path,
		                Name = updateName,
		                InProjectFile = true,
		                OnDisk = true,
                        Properties = itemProperties,
                    };

		            fileReference.OnDisk = discoveredFiles.ContainsKey(path.ToLowerInvariant());
		            discoveredFiles[path.ToLowerInvariant()] = fileReference;
		        }
		    }

		    // Include explicitly included files
		    var itemGroupIncludeNodes = projectNode.Elements("ItemGroup")
		        .SelectMany(element => element.Elements())
		        .Where(element => element.Attribute("Include")?.Value != null)
		        .Where(element => element.Name.LocalName != "Reference" && element.Name.LocalName != "ProjectReference")
		        .ToArray();

		    foreach (var itemGroupIncludeNode in itemGroupIncludeNodes)
		    {
		        var includeName = itemGroupIncludeNode.Attribute("Include")?.Value;
		        var includeType = itemGroupIncludeNode.Name.LocalName;

		        var isPureProjectItem = (new[] { "WCFMetadata", "Folder", "Service", "BootstrapperPackage" }).Contains(includeType);

		        var itemProperties = itemGroupIncludeNode.HasElements ?
		            itemGroupIncludeNode.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value) :
		            new Dictionary<string, string>();

		        if (!isPureProjectItem)
		        {
		            var path = PathExtensions.GetAbsolutePath(this.FolderPath, includeName);
		            var fileReference = new FileReference()
		            {
		                IncludeType = includeType,
		                Path = path,
		                Name = includeName,
		                InProjectFile = true,
		                OnDisk = true,
		                Properties = itemProperties,
		            };

		            fileReference.OnDisk = discoveredFiles.ContainsKey(path.ToLowerInvariant());
		            discoveredFiles[path.ToLowerInvariant()] = fileReference;
		        }
		    }

            // Remove excluded files
            var itemGroupRemoveNodes = projectNode.Elements("ItemGroup")
		        .SelectMany(element => element.Elements())
		        .Where(element => element.Attribute("Remove")?.Value != null)
		        .Where(element => element.Name.LocalName != "Reference" && element.Name.LocalName != "ProjectReference")
		        .ToArray();

		    foreach (var itemGroupRemoveNode in itemGroupRemoveNodes)
		    {
		        var removeName = itemGroupRemoveNode.Attribute("Remove")?.Value;
		        var removeType = itemGroupRemoveNode.Name.LocalName;

		        var isPureProjectItem = (new[] { "WCFMetadata", "Folder", "Service", "BootstrapperPackage" }).Contains(removeType);

		        if (!isPureProjectItem)
		        {
		            var path = PathExtensions.GetAbsolutePath(this.FolderPath, removeName);
		            var fileReference = new FileReference()
		            {
		                IncludeType = "Remove",
		                Path = path,
		                Name = removeName,
		                InProjectFile = true,
                        OnDisk = true,
		            };

		            fileReference.OnDisk = discoveredFiles.ContainsKey(path.ToLowerInvariant());
		            discoveredFiles[path.ToLowerInvariant()] = fileReference;
		        }
		    }

            // Remove wildcards
            var wildCards = new Dictionary<Regex, string>();            
		    foreach (var discoveredFilesKey in discoveredFiles.Keys.ToArray())
		    {
		        if (discoveredFilesKey.IndexOf('*') != -1)
		        {
		            // This is a wildcard

                    // build regex
		            var wildcardFile = discoveredFiles[discoveredFilesKey];
		            var wildcardPath = wildcardFile.Path;

		            var wildcardPattern = wildcardPath.Replace("\\", "@@BACKSLASH@@").Replace("." ,"@@DOT@@").Replace("**", "@@GLOB@@").Replace("*", "@@ANY@@")
		                .Replace("@@BACKSLASH@@", @"\\").Replace("@@DOT@@", @".").Replace("@@GLOB@@", @".*").Replace("@@ANY@@", @"[^\]*");

		            var regex = new Regex(wildcardPattern, RegexOptions.IgnoreCase);
		            wildCards.Add(regex, wildcardFile.IncludeType);

		            discoveredFiles.Remove(discoveredFilesKey);
		        }
		    }
		    foreach (var wildCard in wildCards)
		    {
                
		        foreach (var discoveredFilesKey in discoveredFiles.Keys)
		        {
		            var match = wildCard.Key.Match(discoveredFilesKey);
		            if (match.Success)
		            {
		                if (wildCard.Value == "Remove")
		                {
		                    var discoveredFile = discoveredFiles[discoveredFilesKey];
		                    if (!discoveredFile.InProjectFile && discoveredFile.OnDisk)
		                    {
		                        discoveredFile.IncludeType = "Remove";
		                    }
		                }
                    }
		        }
		    }

            return discoveredFiles.Values;
		}

	    public IDictionary<string, string> GetProjectProperties(string configuration, string platform)
	    {
	        if (!_isCpsDocument)
	        {
	            return GetClassicProjectProperties(configuration, platform);
	        }
	        else
	        {
	            return GetCPSProjectProperties(configuration, platform);
	        }
        }

	    private bool CheckCondition(string conditionExpression, IDictionary<string, string> variables)
	    {
	        if (conditionExpression == null) return true;
	        try
	        {
	            var projectConditionMatchEvaluator = new ProjectConditionMatchEvaluator(variables);
	            var replacedExpression = _replaceProjectVarablesInCondition.Replace(conditionExpression,
	                new MatchEvaluator(projectConditionMatchEvaluator.Evaluate));

	            var expressionLeftRight = _parseProjectCondition.Match(replacedExpression);
	            var left = expressionLeftRight.Groups["left"].Value;
	            var op = expressionLeftRight.Groups["operator"].Value;
	            var right = expressionLeftRight.Groups["right"].Value;

	            if (op == "==")
	            {
	                return left.Equals(right);
	            }
	            if (op == "!=")
	            {
	                return !left.Equals(right);
	            }
	        }
            catch (Exception) { }

            return false;
	    }

	    private class ProjectConditionMatchEvaluator
	    {
	        private readonly IDictionary<string, string> _variables;

	        public ProjectConditionMatchEvaluator(IDictionary<string, string> variables)
	        {
	            _variables = variables;
	        }

	        public string Evaluate(Match match)
	        {
	            if (match.Groups["varable"].Success)
	            {
	                var variableName = match.Groups["varable"].Value;
	                if (_variables.ContainsKey(variableName))
	                {
	                    return _variables[variableName];
	                }
	            }
	            return match.Value;
	        }
	    }

	    private IDictionary<string, string> GetClassicProjectProperties(string configuration, string platform)
	    {
	        var projectNode = _document.Element(_msbNs + "Project");
	        if (projectNode == null) throw new ApplicationException($"Invalid Project xml, could not find Project node");

	        var variables = new Dictionary<string, string>() { { "Configuration", configuration }, { "Platform", platform } };
	        var properties =  projectNode.Elements(_msbNs + "PropertyGroup")
	            .Where(element => CheckCondition(element.Attribute("Condition")?.Value, variables))
	            .Elements()
	            .ToDictionary(e => e.Name.LocalName, e => e.Value);

            return properties;
	    }

        private IDictionary<string, string> GetCPSProjectProperties(string configuration, string platform)
	    {
	        var projectNode = _document.Element("Project");
	        if (projectNode == null) throw new ApplicationException($"Invalid Project xml, could not find Project node");

	        var variables = new Dictionary<string, string>() { { "Configuration", configuration }, { "Platform", platform } };
	        var properties = projectNode.Elements("PropertyGroup")
	            .Where(element => CheckCondition(element.Attribute("Condition")?.Value, variables))
	            .Elements()
	            .ToDictionary(e => e.Name.LocalName, e => e.Value);

            // Add implicit properties
	        if (!properties.ContainsKey("AssemblyName"))
	        {
	            properties.Add("AssemblyName", Name);   
	        }

	        return properties;
	    }


        private void ConvertClassicToCPSProject()
		{
			var projectNode = _document.Element(_msbNs + "Project");
			if (projectNode == null) throw new ApplicationException($"Invalid Project xml, could not find Project node");

			var projectTargetVersion = (projectNode.Elements(_msbNs + "TargetFrameworkVersion").FirstOrDefault()?.Value ?? "v4.5.1").RemoveFromStart("v");

			var itemGroupReferenceNodes = projectNode.Elements(_msbNs + "ItemGroup")
				.SelectMany(element => element.Elements(_msbNs + "Reference"))
				.ToArray();

			var references = new List<ProjectReference>();// GetReferences().OrderBy(r => r).ToArray();
			foreach (var referenceNode in itemGroupReferenceNodes)
			{
				var fullName = referenceNode.Attribute("Include")?.Value;
				var hintPath = referenceNode.Element(_msbNs + "HintPath")?.Value;
				var nameComponents = fullName.Split(',');
				var name = nameComponents[0];
				var version = nameComponents.Length > 1 ? nameComponents[1]?.RemoveFromStart(" Version=") : projectTargetVersion;
				var isPrivate = referenceNode.Element(_msbNs + "Private")?.Value;
				var isPackageReference = IsHintPathPackageReference(hintPath);
				var packageName = isPackageReference ? GetPackageNameFromHintPath(hintPath) : null;

				var projectReference = new ProjectReference()
				{
					Source = this.Name,
					ReferenceType = isPackageReference ? ProjectReferenceType.NuGet : (hintPath != null ? ProjectReferenceType.DLL : ProjectReferenceType.GAC),
					Name = name,
					FullName = fullName,
					HintPath = hintPath,
					Version = new ReferenceVersion(version),
					Private = (isPrivate == "True"),
					IsPackageReference = isPackageReference,
					PackageName = packageName,
					PackageVersion = isPackageReference ? new ReferenceVersion(hintPath) : null,
				};


				references.Add(projectReference);
			}





		}


        public void AddFileToProject(string path, string includeType, IDictionary<string, string> properties = null)
        {
            if (!_isCpsDocument)
            {
                AddFileToClassicProject(path, includeType, properties);
            }
            else
            {
                AddFileToCPSProject(path, includeType, properties);
            }
        }

        private void AddFileToClassicProject(string path, string includeType, IDictionary<string, string> properties)
        {
            var projectNode = _document.Element(_msbNs + "Project");
            if (projectNode == null) throw new ApplicationException($"Invalid Project xml, could not find Project node");

            var include = PathExtensions.MakeRelativePath(this.FolderPath, path);
            var fileType = System.IO.Path.GetExtension(include);

            var isCompileFile = (fileType == ".cs");
            var isNoneFile = (fileType == ".json" || fileType == ".config");

            includeType = includeType ?? (isCompileFile ? "Compile" : "None");

            var itemGroups = projectNode.Elements(_msbNs + "ItemGroup")
                .SelectMany(element => element.Elements(_msbNs + includeType));
            XElement parentNode = null;
            var existingItemGroup = itemGroups.FirstOrDefault();
            if (existingItemGroup != null)
            {
                parentNode = existingItemGroup.Parent;
            }
            else
            {
                var itemGroupElement = new XElement(_msbNs + "ItemGroup");
                projectNode.Add(itemGroupElement);
                parentNode = itemGroupElement;
            }

            var includePath = PathExtensions.MakeRelativePath(this.FolderPath, path);

            var includeElement = projectNode
                .Elements(_msbNs + "ItemGroup")
                .SelectMany(e => e.Elements(_msbNs + includeType))
                .FirstOrDefault(e => e?.Attribute("Include")?.Value == includePath);
            if (includeElement == null)
            {
                includeElement = new XElement(_msbNs + includeType,
                    new XAttribute("Include", includePath)
                );
                parentNode.Add(includeElement);
                _didUpdateDocument = true;
            }

            var itemProperties = includeElement.HasElements
                ? includeElement.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value)
                : new Dictionary<string, string>();

            if (!itemProperties.AreEquivalent(properties))
            {
                if (properties != null)
                {
                    foreach (var property in properties)
                    {
                        var propertyElement = new XElement(_msbNs + property.Key, property.Value);
                        includeElement.Add(propertyElement);
                    }
                }
            }
        }

        private void AddFileToCPSProject(string path, string includeType, IDictionary<string, string> properties)
        {
            var projectNode = _document.Element("Project");
            if (projectNode == null) throw new ApplicationException($"Invalid Project xml, could not find Project node");

            var include = PathExtensions.MakeRelativePath(this.FolderPath, path);
            var fileType = System.IO.Path.GetExtension(include);

            var isCompileFile = (fileType == ".cs");
            var isContentFile = (fileType == ".json" || fileType == ".config");
            var action = (isCompileFile || isContentFile) ? "Update" : "Include";

            includeType = includeType ?? (isCompileFile ? "Compile" : "None");

            //var includePath = PathExtensions.MakeRelativePath(this.FolderPath, path);

            var updateElement = projectNode
                .Elements(_msbNs + "ItemGroup")
                .SelectMany(e => e.Elements(includeType))
                .FirstOrDefault(e => e?.Attribute(action)?.Value == include);

            var removeElement = projectNode
                .Elements("ItemGroup")
                .SelectMany(e => e.Elements(includeType))
                .FirstOrDefault(e => e?.Attribute("Remove")?.Value == include);

            if (updateElement == null && removeElement == null && action  == "Update" && (properties == null || !properties.Any())) return;

            removeElement?.Remove();

            var itemGroups = projectNode.Elements("ItemGroup")
                .SelectMany(element => element.Elements(includeType));
            XElement parentNode = null;
            var existingItemGroup = itemGroups.FirstOrDefault();
            if (existingItemGroup != null)
            {
                parentNode = existingItemGroup.Parent;
            }
            else
            {
                var itemGroupElement = new XElement("ItemGroup");
                projectNode.Add(itemGroupElement);
                parentNode = itemGroupElement;
                _didUpdateDocument = true;
            }

            if (updateElement == null)
            {
                updateElement = new XElement(includeType,
                    new XAttribute(action, include)
                );
                parentNode.Add(updateElement);
                _didUpdateDocument = true;
            }



            var itemProperties = updateElement.HasElements
                ? updateElement.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value)
                : new Dictionary<string, string>();

            if (!itemProperties.AreEquivalent(properties))
            {
                updateElement.Elements().Remove();
                if (properties != null)
                {
                    foreach (var property in properties)
                    {
                        var propertyElement = new XElement(_msbNs + property.Key, property.Value);
                        updateElement.Add(propertyElement);
                    }
                }
                _didUpdateDocument = true;
            }
        }


        public void RemoveFileFromProject(string path)
        {
            if (!_isCpsDocument)
            {
                RemoveFileFromClassicProject(path);
            }
            else
            {
                RemoveFileFromCPSProject(path);
            }
        }

        private void RemoveFileFromClassicProject(string path)
        {
            var projectNode = _document.Element(_msbNs + "Project");
            if (projectNode == null) throw new ApplicationException($"Invalid Project xml, could not find Project node");

            var includePath = PathExtensions.MakeRelativePath(this.FolderPath, path);

            var includeElement = projectNode
                .Elements(_msbNs + "ItemGroup")
                .SelectMany(e => e.Elements())
                .FirstOrDefault(e => e?.Attribute("Include")?.Value == includePath);
            if (includeElement != null)
            {
                includeElement.Remove();
                _didUpdateDocument = true;
            }
        }

        private void RemoveFileFromCPSProject(string path)
        {
            var projectNode = _document.Element("Project");
            if (projectNode == null) throw new ApplicationException($"Invalid Project xml, could not find Project node");

            var include = PathExtensions.MakeRelativePath(this.FolderPath, path);
            var fileType = System.IO.Path.GetExtension(include);
            var isCompileFile = (fileType == ".cs");
            var isNoneFile = (fileType == ".json" || fileType == ".config");
            var elementType = isCompileFile ? "Compile" : "None";

            var updateElement = projectNode
                .Elements(_msbNs + "ItemGroup")
                .SelectMany(e => e.Elements())
                .FirstOrDefault(e => e?.Attribute("Update")?.Value == include);

            var removeElement = projectNode
                .Elements("ItemGroup")
                .SelectMany(e => e.Elements())
                .FirstOrDefault(e => e?.Attribute("Remove")?.Value == include);

            if (updateElement == null && removeElement != null) return;

            XElement parentNode = updateElement?.Parent ?? removeElement?.Parent;
            updateElement?.Remove();

            if (parentNode == null)
            {
                var itemGroups = projectNode.Elements("ItemGroup")
                    .SelectMany(element => element.Elements("Compile"));
                var existingItemGroup = itemGroups.FirstOrDefault();
                if (existingItemGroup != null)
                {
                    parentNode = existingItemGroup.Parent;
                }
                else
                {
                    var itemGroupElement = new XElement("ItemGroup");
                    projectNode.Add(itemGroupElement);
                    parentNode = itemGroupElement;
                    _didUpdateDocument = true;
                }
            }

            if (removeElement == null)
            {
                removeElement = new XElement(elementType,
                    new XAttribute("Remove", include)
                );
                parentNode.Add(removeElement);
                _didUpdateDocument = true;
            }            
        }

	    public string GetProjectOutputPath(string configuration, string platform)
	    {
	        if (!_isCpsDocument)
	        {
	            return GetClassicProjectOutputPath(configuration, platform);
	        }
	        else
	        {
	            return GetCPSProjectOutputPath(configuration, platform);
	        }
	    }

	    private string GetDefaultOutputPath(string configuration, string platform)
	    {
	        return (string.IsNullOrWhiteSpace(platform) || platform == "AnyCPU")
	            ? $"bin\\{configuration}"
	            : $"bin\\{platform}\\{configuration}";
	    }


        private string GetClassicProjectOutputPath(string configuration, string platform)
	    {
	        var projectProperties = GetClassicProjectProperties(configuration, platform);
	        if (projectProperties.ContainsKey("OutputPath"))
	        {
	            return projectProperties["OutputPath"];
	        }
	        return GetDefaultOutputPath(configuration, platform);
	    }

	    private string GetCPSProjectOutputPath(string configuration, string platform)
	    {
	        var projectProperties = GetCPSProjectProperties(configuration, platform);

            var outputPath = projectProperties.Get("OutputPath") ?? GetDefaultOutputPath(configuration, platform);
            var targetFramework = projectProperties.Get("TargetFramework");
	        var platformTarget = projectProperties.Get("PlatformTarget");
	        var runtimeIdentifier = projectProperties.Get("RuntimeIdentifier");

	        if (targetFramework != null)
	        {
	            outputPath = $"{outputPath}\\{targetFramework}";
	        }

	        if (runtimeIdentifier != null)
	        {
	            outputPath = $"{outputPath}\\{runtimeIdentifier}";
            }

	        return outputPath;
	    }


    }
}