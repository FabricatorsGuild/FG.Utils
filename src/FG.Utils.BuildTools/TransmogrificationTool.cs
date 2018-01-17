using System;
using System.Collections.Generic;
using System.Linq;

namespace FG.Utils.BuildTools
{
	public class TransmogrificationTool
	{
		private readonly ILogger _logger;

		public TransmogrificationTool(ILogger logger)
		{
			_logger = logger;
		}

		public static IEnumerable<FolderProjectItem> GetMatchingTargets(SolutionTool solutionTool, FolderTool folderTool, ILogger logger)
		{
			var matchingTargets = new List<FolderProjectItem>();
			var projects = solutionTool.GetProjectsWithParents().ToArray();
			var targetProjects = folderTool.GetProjects();
			var targetProjectsLookup = targetProjects.ToDictionary(p => p.Name, p => p);
			foreach (var project in projects)
			{
				if (solutionTool.CompilableProjects.Contains(project.Type))
				{
					var projectPath = PathExtensions.GetAbsolutePath(solutionTool.FolderPath, project.Path);

					var projectTool = new ProjectTool(projectPath, logger);
					foreach (var reference in projectTool.GetReferences())
					{
						if (targetProjectsLookup.ContainsKey(reference.Name))
						{
							var matchingProject = targetProjectsLookup[reference.Name];

							var nugetPackagesConfigPath = PathExtensions.GetAbsolutePath(projectTool.FolderPath, "packages.config");

							if (System.IO.File.Exists(nugetPackagesConfigPath))
							{
								var nugetTool = new NugetTool(nugetPackagesConfigPath);

								var packages = nugetTool.GetNugetPackages();

								var referencePackage = packages.FirstOrDefault(pkg => pkg.Name == reference.Name);

								if (referencePackage != null)
								{

									if (!matchingTargets.Contains(matchingProject))
									{
										matchingTargets.Add(matchingProject);
									}
								}
							}
						}
					}
				}
			}

			return matchingTargets;
		}

		private void UpdateSolutionWithFolderTargets(SolutionTool solutionTool, FolderTool folderTool)
		{
			var matchingTargets = GetMatchingTargets(solutionTool, folderTool, _logger);

			var relativePath = folderTool.FolderPath.RemoveCommonPrefix(solutionTool.FolderPath, System.IO.Path.DirectorySeparatorChar, StringComparison.InvariantCultureIgnoreCase);

			var subFolders = relativePath.Split(new char[] { System.IO.Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

			var addedProjects = new List<SolutionProjectItem>();
			SolutionProjectItem parentFolderProject = null;
			foreach (var subfolder in subFolders)
			{
				parentFolderProject = solutionTool.AddFolderProject(subfolder, parentFolderProject?.Id);
				addedProjects.Add(parentFolderProject);
				_logger.LogProgress();
			}

			solutionTool.AddProjects(matchingTargets, parentFolderProject?.Id);

			solutionTool.Save();
		}

		public void TransmogrifyNugetPackagesToProjects(SolutionTool solutionTool, FolderTool folderTool)
		{
			UpdateSolutionWithFolderTargets(solutionTool, folderTool);

			var projects = solutionTool.GetProjectsWithParents().ToArray();
			var projectsLookup = projects.Where(p => solutionTool.CompilableProjects.Contains(p.Type)).ToDictionary(p => p.Name, p => p);
			foreach (var project in projects)
			{
				if (solutionTool.CompilableProjects.Contains(project.Type))
				{
					var projectPath = PathExtensions.GetAbsolutePath(solutionTool.FolderPath, project.Path);

					var projectTool = new ProjectTool(projectPath, _logger);

					if (projectTool.IsCpsDocument)
					{
						foreach (var packageReference in projectTool.GetPackageReferences().ToArray())
						{
							if (projectsLookup.ContainsKey(packageReference.Name))
							{
								var matchingProject = projectsLookup[packageReference.Name];

								projectTool.RemovePackageReference(packageReference.Name);
								_logger.LogInformation($"Removed NuGet {packageReference.Name} from {project.Name}");

								var absolutePath = PathExtensions.GetAbsolutePath(solutionTool.FolderPath, matchingProject.Path);
								var relativePath = PathExtensions.MakeRelativePath(projectTool.FolderPath, absolutePath);
								projectTool.AddProjectReference(matchingProject.Name, relativePath, matchingProject.Id);
								_logger.LogInformation($"Added ProjectReference {matchingProject.Name} from {relativePath}");

								var nugetPackageTool = new NugetPackageTool(solutionTool.FolderPath, packageReference.Name, packageReference.Version, packageReference.TargetFramework);
								var nuspec = nugetPackageTool.GetNuspec();

								foreach (var dependency in nuspec.Dependencies)
								{
									if (projectsLookup.ContainsKey(dependency.Name))
									{
										matchingProject = projectsLookup[dependency.Name];
										absolutePath = PathExtensions.GetAbsolutePath(solutionTool.FolderPath, matchingProject.Path);
										relativePath = PathExtensions.MakeRelativePath(projectTool.FolderPath, absolutePath);
										projectTool.AddProjectReference(matchingProject.Name, relativePath, matchingProject.Id);
										_logger.LogInformation($"Added ProjectReference {matchingProject.Name} from {relativePath} for dependency from nuspec");
									}
									else
									{
										projectTool.AddNugetReference(dependency.Name, dependency.Version, null);
									}
									_logger.LogProgress();
								}
							}
							_logger.LogProgress();
						}
					}
					else
					{
						foreach (var reference in projectTool.GetReferences().ToArray())
						{
							if (projectsLookup.ContainsKey(reference.Name))
							{
								var matchingProject = projectsLookup[reference.Name];

								var packages = projectTool.GetPackageReferences().ToArray();
								var referencePackage = packages.FirstOrDefault(pkg => pkg.Name == reference.Name);

								if (referencePackage != null)
								{
									projectTool.RemovePackageReference(referencePackage.Name);
									_logger.LogInformation($"Removed NuGet {referencePackage.Name} from packages.config");

									projectTool.RemoveReference(reference.Name);
									_logger.LogInformation($"Removed Reference {reference.Name} from {projectTool.FilePath}");

									var absolutePath = PathExtensions.GetAbsolutePath(solutionTool.FolderPath, matchingProject.Path);
									var relativePath = PathExtensions.MakeRelativePath(projectTool.FolderPath, absolutePath);
									projectTool.AddProjectReference(matchingProject.Name, relativePath, matchingProject.Id);
									_logger.LogInformation($"Added ProjectReference {matchingProject.Name} from {relativePath}");

									var nugetPackageTool =
										new NugetPackageTool(solutionTool.FolderPath, referencePackage.Name, referencePackage.Version, referencePackage.TargetFramework);
									var nuspec = nugetPackageTool.GetNuspec();

									foreach (var dependency in nuspec.Dependencies)
									{
										if (projectsLookup.ContainsKey(dependency.Name))
										{
											matchingProject = projectsLookup[dependency.Name];
											absolutePath = PathExtensions.GetAbsolutePath(solutionTool.FolderPath, matchingProject.Path);
											relativePath = PathExtensions.MakeRelativePath(projectTool.FolderPath, absolutePath);
											projectTool.AddProjectReference(matchingProject.Name, relativePath, matchingProject.Id);
											_logger.LogInformation($"Added ProjectReference {matchingProject.Name} from {relativePath} for dependency from nuspec");

										}
										else
										{
											var existingReference = packages.FirstOrDefault(pkg => pkg.Name == dependency.Name);
											if (existingReference == null)
											{
												projectTool.AddNugetReference(dependency.Name, dependency.Version, projectTool.GetTargetFramework());
											}
										}
										_logger.LogProgress();
									}
								}
							}
							_logger.LogProgress();
						}
					}

					projectTool.Save();
				}
				_logger.LogProgress();
			}
		}

		public void ReTransmogrifyProjectsToNugetPackages(SolutionTool solutionTool, FolderTool folderTool)
		{
			var projectsToRemove = folderTool.GetProjects();

			var solutionProjectItems = solutionTool.GetProjectsWithParents().ToArray();
			var solutionProjectsToRemove = solutionProjectItems.Where(p => projectsToRemove.Any(p2 => p2.Name == p.Name)).ToArray();

			var possibleParentsToKill = new List<SolutionProjectItem>();
			foreach (var solutionProjectToRemove in solutionProjectsToRemove)
			{
				if (solutionProjectToRemove.ParentId != Guid.Empty)
				{
					var parentProject = solutionProjectItems.Single(p => p.Id == solutionProjectToRemove.ParentId);
					if (!possibleParentsToKill.Contains(parentProject))
					{
						possibleParentsToKill.Add(parentProject);
					}
				}
				_logger.LogInformation($"Remove {solutionProjectToRemove.Name}");
				_logger.LogProgress();
			}

			var dependentProjects = solutionProjectItems.Where(p =>
				solutionProjectsToRemove.All(p2 => p2.Name != p.Name) &&
				(p.Type == SolutionTool.ProjectTypeIdClassLibrary || p.Type == SolutionTool.ProjectTypeIdWebProject));

			foreach (var dependentProject in dependentProjects)
			{
				var projectPath = System.IO.Path.Combine(solutionTool.FolderPath, dependentProject.Path);
				var projectTool = new ProjectTool(projectPath, _logger);

				var projectReferences = projectTool.GetProjectReferences().ToArray();
				var packageReferences = projectTool.GetPackageReferences().ToArray();

				_logger.LogInformation($"References in project {dependentProject.Name}");
				var dependencies = new List<NugetPackage>();

				var projectReferencesToRemove = projectReferences.Where(pr => solutionProjectsToRemove.Any(p => p.Name == pr.Name)).ToArray();
				foreach (var projectReference in projectReferencesToRemove)
				{
					var projectReferencePath = PathExtensions.GetAbsolutePath(projectTool.FolderPath, projectReference.HintPath);
					var projectReferenceTool = new ProjectTool(projectReferencePath, _logger);

					var targetFramework = projectReferenceTool.GetTargetFramework();
					var nugetPackageTool = NugetPackageTool.GetNugetPackageTool(solutionTool.FolderPath, projectReference.Name, targetFramework);
					if (nugetPackageTool != null)
					{
						var nuspec = nugetPackageTool.GetNuspec();

						_logger.LogInformation($"\t{projectReference.Name} - {projectReference.HintPath} - {nuspec.Name} {nuspec.Version}");

						foreach (var dependency in nuspec.Dependencies)
						{
							if (dependencies.All(a => a.Name != dependency.Name) &&
							    packageReferences.All(r => r.Name != dependency.Name) &&
							    solutionProjectsToRemove.All(p => p.Name != dependency.Name))
							{
								dependencies.Add(new NugetPackage { Name = dependency.Name, Version = dependency.Version, TargetFramework = targetFramework });
								_logger.LogInformation($"\t\tSub Nuget dependency {dependency.Name} {dependency.Version}");
							}
						}
						_logger.LogProgress();

						if (dependencies.All(a => a.Name != nuspec.Name) &&
						    packageReferences.All(r => r.Name != nuspec.Name))
						{
							dependencies.Add(new NugetPackage { Name = nuspec.Name, Version = nuspec.Version.ToString(), TargetFramework = targetFramework });
						}

						projectTool.RemoveProjectReference(projectReference);
						projectTool.AddNugetReference(nuspec.Name, nuspec.Version.ToString(), nugetPackageTool.TargetFrameworkVersion);

					}
					else
					{
						_logger.LogInformation($"\t{projectReference.Name} - {projectReference.HintPath} - NO NUGET");
					}
					_logger.LogProgress();
				}

				foreach (var dependency in dependencies)
				{
					var nugetPackageTool = new NugetPackageTool(solutionTool.FolderPath, dependency.Name, dependency.Version, dependency.TargetFramework);

					var libReferences = nugetPackageTool.GetLibPaths();
					foreach (var libReference in libReferences)
					{
						projectTool.AddReference(libReference);
						_logger.LogProgress();
					}
					_logger.LogProgress();
				}

				projectTool.Save();
			}

			foreach (var solutionProjectToRemove in solutionProjectsToRemove)
			{
				solutionTool.RemoveProject(solutionProjectToRemove);
				_logger.LogProgress();
			}
			solutionTool.Save();
		}
	}
}