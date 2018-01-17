using System.Collections.Generic;
using System.Linq;

namespace FG.Utils.BuildTools
{
	public class CleanupTool
	{
		private readonly ILogger _logger;

		public CleanupTool(ILogger logger)
		{
			_logger = logger;
		}

		public void CleanUpReferencesInProjectFile(SolutionTool solutionTool)
		{
			var projects = solutionTool.GetProjectsWithParents().ToArray();
			foreach (var project in projects)
			{
				if (solutionTool.CompilableProjects.Contains(project.Type))
				{
					var projectPath = PathExtensions.GetAbsolutePath(solutionTool.FolderPath, project.Path);

					var projectTool = new ProjectTool(projectPath, _logger);
					projectTool.CleanUpProject();
					_logger.LogMessage($"Cleaned {projectTool.Name}");
					projectTool.Save();
				}
			}
		}

		public void ScanAllReferencesInProjectFiles(SolutionTool solutionTool)
		{
			var projects = solutionTool.GetProjectsWithParents().ToArray();
			var references = new List<ProjectReference>();
			foreach (var project in projects)
			{
				if (solutionTool.CompilableProjects.Contains(project.Type))
				{
					var projectPath = PathExtensions.GetAbsolutePath(solutionTool.FolderPath, project.Path);

					var projectTool = new ProjectTool(projectPath, _logger);
					references.AddRange(projectTool.ScanReferences());
					_logger.LogMessage($"Scanned {projectTool.Name}");
				}
			}

			foreach (var referenceName in references.GroupBy(r => r.Name).OrderBy(r => r.Key))
			{

				var referenceVersions = referenceName.GroupBy(r => $"{r.PackageVersion?.ToString()}-{r.ReferenceType}");
				//var referenceVersions = referenceName.GroupBy(r => $"{r.Version?.ToString()}/{r.PackageVersion?.ToString()}-{r.ReferenceType}");
				if (referenceVersions.Count() > 1)
				{
					_logger.LogMessage($"\t{referenceName.Key}");

					foreach (var referenceVersion in referenceVersions)
					{
						_logger.LogMessage($"\t\t{referenceVersion.Key}");

						foreach (var reference in referenceVersion)
						{
							_logger.LogMessage(
								$"\t\t\t{reference.Source} - {reference.Version}/{reference.PackageVersion} {reference.ReferenceType} {reference.PackageName}");
						}
					}
				}
				else
				{
					//_logger.LogMessage($"\t{referenceName.Key} - {referenceVersions.FirstOrDefault().Key} - {referenceName.Count()} references");
				}
			}

			// Check different version of the same reference

			// Check different types of references with the same name


		}

		public void ScanAllFilesInProjectFolders(SolutionTool solutionTool)
		{
			var projects = solutionTool.GetProjectsWithParents().ToArray();
			foreach (var project in projects)
			{
				if (solutionTool.CompilableProjects.Contains(project.Type))
				{
					var projectPath = PathExtensions.GetAbsolutePath(solutionTool.FolderPath, project.Path);

					var projectTool = new ProjectTool(projectPath, _logger);
					var fileReferences = projectTool.ScanFilesInProjectFolder();
					var filesNotInProject = fileReferences.Where(f => f.OnDisk && !f.InProjectFile).ToArray();
					var filesNotOnDisk = fileReferences.Where(f => !f.OnDisk && f.InProjectFile).ToArray();

					if (filesNotOnDisk.Any() || filesNotInProject.Any())
					{
						_logger.LogMessage($"Scanned files in {projectTool.Name}");

						if (filesNotInProject.Any())
						{
							_logger.LogMessage($"\tFiles NOT in {projectTool.Name}");
							foreach (var fileReference in fileReferences.Where(f => f.OnDisk && !f.InProjectFile))
							{
								_logger.LogMessage($"\t\t{fileReference.Name}");
							}
						}

						if (filesNotOnDisk.Any())
						{
							_logger.LogMessage($"\tFiles NOT on disk");
							foreach (var fileReference in fileReferences.Where(f => !f.OnDisk && f.InProjectFile))
							{
								_logger.LogMessage($"\t\t[{fileReference.IncludeType}] {fileReference.Name}");

							}
						}
					}
				}
			}			
		}

		public void RemoveFilesNotIncludedInProjects(SolutionTool solutionTool)
		{
			var projects = solutionTool.GetProjectsWithParents().ToArray();
			foreach (var project in projects)
			{
				if (solutionTool.CompilableProjects.Contains(project.Type))
				{
					var projectPath = PathExtensions.GetAbsolutePath(solutionTool.FolderPath, project.Path);

					var projectTool = new ProjectTool(projectPath, _logger);
					var fileReferences = projectTool.ScanFilesInProjectFolder();
					var filesNotInProject = fileReferences.Where(f => f.OnDisk && !f.InProjectFile).ToArray();
					var filesNotOnDisk = fileReferences.Where(f => !f.OnDisk && f.InProjectFile).ToArray();

					if (filesNotOnDisk.Any() || filesNotInProject.Any())
					{
						_logger.LogMessage($"Scanned files in {projectTool.Name}");

						if (filesNotInProject.Any())
						{
							_logger.LogMessage($"\tFiles NOT in {projectTool.Name}");
							foreach (var fileReference in fileReferences.Where(f => f.OnDisk && !f.InProjectFile))
							{
								_logger.LogMessage($"\t\t{fileReference.Name}");

								System.IO.File.Delete(fileReference.Path);
							}
						}
					}
				}
			}
		}
	}
}