using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace FG.Utils.BuildTools
{
	public class SolutionTool
	{
		private readonly string _solutionPath;
		private string _solutionData;
		private string SolutionData => _solutionData ?? (_solutionData = System.IO.File.ReadAllText(_solutionPath).Replace("\r\n", "\n"));

		private void SetSolutionData(string data)
		{
			_solutionData = data;
			_didChangeFile = true;
		}

		private bool _didChangeFile = false;

		public string FilePath => _solutionPath;

		public string FolderPath => System.IO.Path.GetDirectoryName(_solutionPath);

		public static Guid ProjectTypeIdServiceFabricApplication = new Guid("a07b5eb6-e848-4116-a8d0-a826331d98c6");
		public static Guid ProjectTypeIdSolutionFolder = new Guid("2150e333-8fdc-42a3-9474-1a3956d46de8");
		public static Guid ProjectTypeIdClassLibrary = new Guid("fae04ec0-301f-11d3-bf4b-00c04f79efbc");
		public static Guid ProjectTypeIdWebProject = new Guid("9a19103f-16f7-4668-be54-9a1e7a4f7556");

		private readonly IDictionary<Guid, string> _projectTypeMapping = new Dictionary<Guid, string>()
		{
			{ProjectTypeIdServiceFabricApplication, "Service Fabric Application"},
			{ProjectTypeIdSolutionFolder, "Solution Folder" },
			{ProjectTypeIdClassLibrary, "Class Library" },
			{ProjectTypeIdWebProject, "Web Project" }
		};

		public IEnumerable<Guid> CompilableProjects =>
			new[] {new Guid("fae04ec0-301f-11d3-bf4b-00c04f79efbc"), new Guid("9a19103f-16f7-4668-be54-9a1e7a4f7556")};

		public string GetProjectTypeName(Guid id)	
		{
			return _projectTypeMapping.ContainsKey(id) ? _projectTypeMapping[id] : id.ToString();
		}

		private static readonly string _findSolutionProjectRegexPattern = @"Project\(\""{%%PROJECTTYPEID%%}\""\) = \""%%PROJECTNAME%%\"", \""(?<projectpath>[^""]*)\"", \""\{(?<projectid>[A-F0-9]{8}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{12})\}\""\s*EndProject";
		private static readonly string _findSolutionProjectRelationshipRegexPattern = @"[ \t]*GlobalSection\(NestedProjects\) = preSolution(?>\s*{[A-F0-9]{8}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{12}} = {[A-F0-9]{8}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{12}})*[ \t]*\n[ \t]*(?<relationship>{%%PROJECTID%%} = {%%PROJECTPARENTID%%})(?>\s*{[A-F0-9]{8}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{12}} = {[A-F0-9]{8}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{12}})*\n[ \t]*EndGlobalSection";
		private static readonly string _findSolutionProjectRelationshipsRegexPattern = @"[ \t]*GlobalSection\(NestedProjects\) = preSolution(?>\s*{[A-F0-9]{8}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{12}} = {[A-F0-9]{8}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{12}})*[ \t]*(?<relationshipLine>\n[ \t]*(?<relationship>{%%PROJECTID%%} = {(?<projectparentid>[A-F0-9]{8}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{12})}))(?>\s*{[A-F0-9]{8}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{12}} = {[A-F0-9]{8}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{12}})*\n[ \t]*EndGlobalSection";

		private static readonly Regex _findSolutionProjects = new Regex(
			@"Project\(\""{(?<projecttypeid>[A-F0-9]{8}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{12})}\""\) = ""(?<projectname>[^""]+)"", ""(?<projectpath>[^""]+)"", ""{(?<projectid>[A-F0-9]{8}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{12})}""\nEndProject", RegexOptions.Compiled | RegexOptions.IgnoreCase);


		private static readonly string _findSolutionProjectConfigPattern =
			@"[ \t]*\{(?<projectid>%%PROJECTNAME%%)}{1}\.(?<projectconfig>[^\|]+\|[^\.]+)\.(?<item>[^=]+) = (?<solutionconfig>[^\|]+\|[^\n]+)";

		private static readonly Regex _findSolutionProjectConfigs =
			new Regex(@"\{(?<projectid>[A-F0-9]{8}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{12})}{1}\.(?<projectconfig>[^\|]+\|[^\.]+)\.(?<item>[^=]+) = (?<solutionconfig>[^\|]+\|[^\n]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		private static readonly Regex _finsSolutionProjectConfigSections = new Regex(
			@"(?<startsection>GlobalSection\(ProjectConfigurationPlatforms\) = postSolution[ \t]*)\n(?<indent>[ \t]*)(?<configs>(?>[ \t]*{(?>[A-F0-9]{8}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{12})}{1}\.(?>[^\|]+\|[^\.]+)\.(?>[^=]+)=(?>[^\|]+\|[^\n]+)[ \t]*\n)*)(?<endsection>[ \t]*EndGlobalSection)",
			RegexOptions.Compiled | RegexOptions.IgnoreCase); 

		private static readonly Regex _findProjectParents =
			new Regex(
				@"\{(?<projectid>[A-F0-9]{8}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{12})} = {(?<parentprojectid>[A-F0-9]{8}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{12})}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		private static readonly string _findSolutionGlobalSectionProjectRelationship =
				@"(?<indent0>[ \t]*)(?<startline>GlobalSection\(NestedProjects\) \= preSolution)[ \t\r]*\n(?<indent>\s*)(?<projects>(?>\{[A-F0-9]{8}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{12}} = {[A-F0-9]{8}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{12}}\s*)*)(?<endline>EndGlobalSection)"
			;

		private static readonly Regex _findSolutionGlobalSectionProjectRelationships = new Regex(
			@"(?<indent0>[ \t]*)(?<startline>GlobalSection\(NestedProjects\) \= preSolution)[ \t\r]*\n(?<indent>\s*)(?<projects>(?>\{[A-F0-9]{8}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{12}} = {[A-F0-9]{8}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{12}}\s*)*)(?<endline>EndGlobalSection)",
			RegexOptions.Compiled | RegexOptions.IgnoreCase);

		private static readonly Regex _findProjectsDefinitionEnd = new Regex(@"(?<lastprojectend>EndProject\s*)(?<startnextsection>Global\s*GlobalSection\(SolutionConfigurationPlatforms\) = preSolution)", RegexOptions.Compiled);
		
		public SolutionTool(string solutionPath)
		{
			_solutionPath = solutionPath;
		}

		public IEnumerable<SolutionProjectItem> GetProjectsHierarchy()
		{
			var projects = GetProjects().ToDictionary(p => p.Id, p => p);
			var projectsHierarchy = GetProjectsHierarchyInternal().ToArray();
			var updatedProjects = new List<SolutionProjectItem>();
			foreach (var project in projects)
			{
				var projectHierarchy = projectsHierarchy.FirstOrDefault(p => p.Id.Equals(project.Key));
				if (projectHierarchy != null)
				{
					project.Value.ParentId = projectHierarchy.ParentId;
				}
				updatedProjects.Add(project.Value);
			}

			var topLevelProjectIds = updatedProjects.Where(p => Guid.Empty.Equals(p.ParentId)).Select(p => p.Id).ToArray();
			foreach (var projectId in topLevelProjectIds)
			{
				yield return GetProjectItemWithChildren(projectId, projects, projectsHierarchy);
			}
		}

		public IEnumerable<SolutionProjectItem> GetProjectsWithParents()
		{
			var projects = GetProjects().ToDictionary(p => p.Id, p => p);
			var projectHierarchy = GetProjectsHierarchyInternal().ToArray();

			foreach (var project in projects)
			{
				yield return GetProjectItemWithChildren(project.Key, projects, projectHierarchy);
			}
		}

		private SolutionProjectItem GetProjectItemWithChildren(Guid projectId, IDictionary<Guid, SolutionProjectItem> projectItems,
			SolutionProjectHierarchy[] projectHierarchy)
		{
			var childProjects = projectHierarchy.Where(p => p.ParentId == projectId).Select(p => GetProjectItemWithChildren(p.Id, projectItems, projectHierarchy));
			var project = projectItems[projectId];
			project.ChildProjects = childProjects.ToArray();

			return project;
		}

		private IEnumerable<SolutionProjectItem> GetProjects()
		{
			var projectMatches = _findSolutionProjects.Match(SolutionData);
			var projectConfigurations = GetProjectConfigurations();

			do
			{
				var solutionProjectItem = new SolutionProjectItem()
				{
					Type = new Guid(projectMatches.Groups["projecttypeid"]?.Value ?? "00000000-0000-0000-0000-000000000000"),
					Id = new Guid(projectMatches.Groups["projectid"]?.Value ?? "00000000-0000-0000-0000-000000000000"),
					Name = projectMatches.Groups["projectname"]?.Value,
					Path = projectMatches.Groups["projectpath"]?.Value,
				};
				solutionProjectItem.TypeName = GetProjectTypeName(solutionProjectItem.Type);
				solutionProjectItem.Configurations = projectConfigurations.Where(c => c.Id == solutionProjectItem.Id).ToArray();

				yield return solutionProjectItem;
				projectMatches = projectMatches.NextMatch();
			} while (projectMatches.Success);
		}

		private IEnumerable<SolutionProjectConfiguration> GetProjectConfigurations()
		{
			var match = _findSolutionProjectConfigs.Match(SolutionData);
			
			do
			{
				var solutionProjectItem = new SolutionProjectConfiguration()
				{
					Id = new Guid(match.Groups["projectid"]?.Value ?? "00000000-0000-0000-0000-000000000000"),
					ProjectConfig = match.Groups["projectconfig"]?.Value,
					ConfigItem = match.Groups["item"]?.Value,
					Solutionconfig = match.Groups["solutionconfig"]?.Value,
				};

				yield return solutionProjectItem;

				match = match.NextMatch();
			} while (match.Success);
		}

		private IEnumerable<SolutionProjectHierarchy> GetProjectsHierarchyInternal()
		{
			var match = _findProjectParents.Match(SolutionData);

			do
			{
				var solutionProjectItem = new SolutionProjectHierarchy()
				{
					Id = new Guid(match.Groups["projectid"]?.Value ?? "00000000-0000-0000-0000-000000000000"),
					ParentId = new Guid(match.Groups["parentprojectid"]?.Value ?? "00000000-0000-0000-0000-000000000000"),
				};

				yield return solutionProjectItem;

				match = match.NextMatch();
			} while (match.Success);
		}

		private SolutionProjectItem GetProject(Guid projectTypeId, string name)
		{
			var pattern = _findSolutionProjectRegexPattern
				.Replace("%%PROJECTTYPEID%%", projectTypeId.ToString())
				.Replace("%%PROJECTNAME%%", name);

			var findProjectRegex = new Regex(pattern, RegexOptions.IgnoreCase);

			var projectMatch = findProjectRegex.Match(SolutionData);
			if(projectMatch.Success)
			{
				var solutionProjectItem = new SolutionProjectItem()
				{
					Type = projectTypeId,
					Id = new Guid(projectMatch.Groups["projectid"]?.Value ?? "00000000-0000-0000-0000-000000000000"),
					Name = name,
					Path = projectMatch.Groups["projectpath"]?.Value,
				};
				solutionProjectItem.TypeName = GetProjectTypeName(solutionProjectItem.Type);

				return solutionProjectItem;
			}

			return null;
		}

		public SolutionProjectItem AddFolderProject(string name, Guid? parentProjectId)
		{
			var item = GetProject(ProjectTypeIdSolutionFolder, name);

			if (item == null)
			{
				item = new SolutionProjectItem()
				{
					Id = Guid.NewGuid(),
					Type = ProjectTypeIdSolutionFolder,
					TypeName = GetProjectTypeName(ProjectTypeIdSolutionFolder),
					Name = name,
					ParentId = parentProjectId ?? Guid.Empty,
					Path = name,
				};
				var match = _findProjectsDefinitionEnd.Match(SolutionData);

				var replaceSection = match.Value;

				var lastprojectend = match.Groups["lastprojectend"]?.Value;
				var startnextsection = match.Groups["startnextsection"]?.Value;

				var projectItemDefinition = $"Project(\"{{{item.Type.ToUpper()}}}\") = \"{item.Name}\", \"{item.Path}\", \"{{{item.Id.ToUpper()}}}\"\nEndProject";
				var output = $"{lastprojectend}{projectItemDefinition}\n{startnextsection}";

				SetSolutionData(SolutionData.Replace(replaceSection, output));
			}

			AddProjectRelationships(new SolutionProjectItem[] {item}, parentProjectId);

			return item;
		}

		private SolutionProjectItem AddProject(Guid projectTypeId, string name, string path, Guid? parentProjectId)
		{
			var item = GetProject(projectTypeId, name);

			if (item == null)
			{
				item = new SolutionProjectItem()
				{
					Id = Guid.NewGuid(),
					Type = projectTypeId,
					TypeName = GetProjectTypeName(projectTypeId),
					Name = name,
					ParentId = parentProjectId ?? Guid.Empty,
					Path = path,
				};
				var match = _findProjectsDefinitionEnd.Match(SolutionData);

				var replaceSection = match.Value;

				var lastprojectend = match.Groups["lastprojectend"]?.Value;
				var startnextsection = match.Groups["startnextsection"]?.Value;

				var projectItemDefinition = $"Project(\"{{{item.Type.ToUpper()}}}\") = \"{item.Name}\", \"{item.Path}\", \"{{{item.Id.ToUpper()}}}\"\nEndProject";
				var output = $"{lastprojectend}{projectItemDefinition}\n{startnextsection}";

				SetSolutionData(SolutionData.Replace(replaceSection, output));
			}

			AddProjectRelationships(new SolutionProjectItem[] { item }, parentProjectId);

			if (projectTypeId == ProjectTypeIdClassLibrary || projectTypeId == ProjectTypeIdWebProject)
			{
				AddProjectConfiguration(item);
			}

			return item;
		}	

		public void AddProjects(IEnumerable<FolderProjectItem> projectItems, Guid? parentItemId)
		{
			SetSolutionData(SolutionData ?? System.IO.File.ReadAllText(_solutionPath).Replace("\r\n", "\n"));

			foreach (var projectItem in projectItems)
			{
				// TODO: Check the type in the actual project
				var relativePath = PathExtensions.MakeRelativePath(FolderPath, projectItem.Path);
				AddProject(ProjectTypeIdClassLibrary, projectItem.Name, relativePath, parentItemId);
			}			
		}

		public void Save()
		{
			if (!_didChangeFile) return;

			System.IO.File.WriteAllText(_solutionPath, SolutionData);
		}

		private bool HasProjectRelationship(SolutionProjectItem projectItem, Guid parentItemId)
		{
			var pattern = _findSolutionProjectRelationshipRegexPattern
				.Replace("%%PROJECTID%%", projectItem.Id.ToUpper())
				.Replace("%%PROJECTPARENTID%%", parentItemId.ToUpper());

			var findProjectRegex = new Regex(pattern, RegexOptions.IgnoreCase);

			var projectMatch = findProjectRegex.Match(SolutionData);
			return projectMatch.Success;
		}

		private void AddProjectRelationships(IEnumerable<SolutionProjectItem> projectItems, Guid? parentItemId)
		{
			if (parentItemId == null) return;

			var match = _findSolutionGlobalSectionProjectRelationships.Match(SolutionData);
			System.Diagnostics.Debug.Assert(match.Success);
			var replaceSection = match.Value;

			var indent0 = match.Groups["indent0"]?.Value;
			var indent = match.Groups["indent"]?.Value;
			var startline = match.Groups["startline"]?.Value;
			var endline = match.Groups["endline"]?.Value;
			var projects = match.Groups["projects"]?.Value;

			var lineBuilder = new StringBuilder();
			foreach (var newProjectItem in projectItems)
			{
				if (!HasProjectRelationship(newProjectItem, parentItemId.Value))
				{
					lineBuilder.Append($"{indent}{{{newProjectItem.Id.ToUpper()}}} = {{{parentItemId.ToUpper()}}}\n");
				}
			}
			if (lineBuilder.Length > 0)
			{
				var output = $"{indent0}{startline}\n{indent}{projects.TrimEnd()}\n{lineBuilder}{indent0}{endline}";
				SetSolutionData(SolutionData.Replace(replaceSection, output));
				_didChangeFile = true;
			}
		}

		private void AddProjectConfiguration(SolutionProjectItem projectItem)
		{
			var configurations = GetProjectConfigurations();

			var existingConfigs = configurations.Where(c => c.Id == projectItem.Id);
			if (existingConfigs.Any()) return;

			var match = _finsSolutionProjectConfigSections.Match(SolutionData);
			System.Diagnostics.Debug.Assert(match.Success);
			var replaceSection = match.Value;

			var indent = match.Groups["indent"]?.Value;
			var startsection = match.Groups["startsection"]?.Value;
			var configs = match.Groups["configs"]?.Value;
			var endsection = match.Groups["endsection"]?.Value;

			var firstConfig = configurations.First();
			var cloneTemplateConfig = configurations.Where(c => c.Id == firstConfig.Id);

			var configBuilder = new StringBuilder();
			foreach (var cloneConfig in cloneTemplateConfig)
			{
				configBuilder.Append($"{indent}{{{projectItem.Id.ToUpper()}}}.{cloneConfig.ProjectConfig}.{cloneConfig.ConfigItem} = {cloneConfig.Solutionconfig}\n");
			}

			var output = $"{startsection}\n{indent}{configs}{configBuilder}{endsection}";

			SetSolutionData(SolutionData.Replace(replaceSection, output));
			_didChangeFile = true;
		}

		private void RemoveProjectConfiguration(SolutionProjectItem projectItem)
		{
			var pattern = _findSolutionProjectConfigPattern
				.Replace("%%PROJECTNAME%%", projectItem.Id.ToUpper());

			var findProjectRegex = new Regex(pattern, RegexOptions.IgnoreCase);

			var projectMatch = findProjectRegex.Match(SolutionData);
			var solutionData = SolutionData;
			while (projectMatch.Success)
			{
				solutionData = solutionData.Replace($"{projectMatch.Value}\n", "");

				projectMatch = projectMatch.NextMatch();
			}
			SetSolutionData(solutionData);

		}

		private void RemoveProjectRelationships(SolutionProjectItem projectItem)
		{
			var pattern = _findSolutionProjectRelationshipsRegexPattern
				.Replace("%%PROJECTID%%", projectItem.Id.ToUpper());

			var findProjectRegex = new Regex(pattern, RegexOptions.IgnoreCase);

			var projectMatch = findProjectRegex.Match(SolutionData);
			var solutionData = SolutionData;
			while (projectMatch.Success)
			{
				var replacement = projectMatch.Groups["relationshipLine"]?.Value;
				solutionData = solutionData.Replace(replacement, "");

				projectMatch = projectMatch.NextMatch();
			}
			SetSolutionData(solutionData);
		}

		public void RemoveProject(SolutionProjectItem projectItem)
		{
			var pattern = _findSolutionProjectRegexPattern
				.Replace("%%PROJECTTYPEID%%", projectItem.Type.ToUpper())
				.Replace("%%PROJECTNAME%%", projectItem.Name);

			var findProjectRegex = new Regex(pattern, RegexOptions.IgnoreCase);

			var projectMatch = findProjectRegex.Match(SolutionData);
			if (projectMatch.Success)
			{
				SetSolutionData(SolutionData.Replace($"{projectMatch.Value}\n", ""));
			}

			RemoveProjectConfiguration(projectItem);

			RemoveProjectRelationships(projectItem);
		}
	}
}