using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimeNetComponents.Controls
{
	internal class ViewingTheme
	{
		public string Name { get; set; }
		public Dictionary<string,string> Variables { get; set; }
		public ViewingTheme()
		{
			Name = string.Empty;
			Variables = new Dictionary<string, string>();			
		}
	}
}
