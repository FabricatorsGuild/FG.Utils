using System;

namespace FG.Utils.BuildTools
{
	public class SolutionProjectItem
	{
		public Guid Type { get; set; }

		public string TypeName { get; set; }
		public Guid Id { get; set; }

		public Guid ParentId { get; set; }

		public string Name { get; set; }
		public string Path { get; set; }

		public SolutionProjectConfiguration[] Configurations { get; set; }

		public SolutionProjectItem[] ChildProjects { get; set; }
	}
}