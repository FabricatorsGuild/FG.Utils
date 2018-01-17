using System.Collections.Generic;

namespace FG.Utils.BuildTools
{
	public class FileReference
	{
	    public FileReference()
	    {
	        Properties = new Dictionary<string, string>();
	    }
		public bool OnDisk { get; set; }
		public bool InProjectFile { get; set; }
		public string Path { get; set; }
		public string Name { get; set; }
		public string IncludeType { get; set; }
        public IDictionary<string, string> Properties { get; set; }
	}
}