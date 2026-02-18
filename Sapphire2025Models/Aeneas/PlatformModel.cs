using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2025Models.Aeneas
{
	public class PlatformModel
	{
		public int Id { get; set; }
		public string StationName { get; set; }
		public string PlatformName { get; set; }

		public PlatformModel()
		{
			Id = 0;	
			StationName=string.Empty;
			PlatformName = string.Empty;
		}
	}
}
