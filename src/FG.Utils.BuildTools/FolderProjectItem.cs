namespace FG.Utils.BuildTools
{
	public class FolderProjectItem
	{
		public string Name { get; set; }
		public string Path { get; set; }

		public override string ToString()
		{
			return $"Folder project: {Name} {Path}";
		}
	}
}