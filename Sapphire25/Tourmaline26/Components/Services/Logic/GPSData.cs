using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;

namespace Tourmaline26.Components.Services.Logic
{
	public class GPSData
	{
		public double Latitude { get; set; }
		public double Longitude { get; set; }
		public DateTime Time{ get; set; }

	}
}
