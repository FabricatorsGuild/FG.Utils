using System.Collections.Generic;

namespace FG.Utils.BuildTools
{
	public class FolderTool
	{
		private readonly string _folderPath;
		public string FolderPath => _folderPath;

		public FolderTool(string folderPath)
		{
			_folderPath = folderPath;
		}

		public IEnumerable<FolderProjectItem> GetProjects()
		{
			var projects = GetProjects(_folderPath);

			return projects;
		}

		private IEnumerable<FolderProjectItem> GetProjects(string folderPath)
		{
			var projects = new List<FolderProjectItem>();

			var projectsInFolder = System.IO.Directory.GetFiles(folderPath, "*.csproj");

			foreach (var projectInFolder in projectsInFolder)
			{
				var folderProjectItem = new FolderProjectItem()
				{
					Name = System.IO.Path.GetFileNameWithoutExtension(projectInFolder),
					Path = projectInFolder,
				};
				projects.Add(folderProjectItem);
			}

			var subFolders = System.IO.Directory.GetDirectories(folderPath);
			foreach (var subFolder in subFolders)
			{
				var subFolderProjects = GetProjects(subFolder);
				projects.AddRange(subFolderProjects);
			}

			return projects;
		}	
	}
}