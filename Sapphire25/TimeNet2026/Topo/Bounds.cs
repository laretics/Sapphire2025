using System;
using System.Collections.Generic;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimeNet2026.Topo
{
	internal struct Bounds
	{
		private GeoLocation[] mcolBounds = new GeoLocation[2];
		double MinimumLatitude { get => mcolBounds[0].Latitude; set => mcolBounds[0].Latitude=value; }
		double MinimumLongitude { get => mcolBounds[0].Longitude; set => mcolBounds[0].Longitude = value; }
		double MaximumLatitude { get => mcolBounds[1].Latitude; set => mcolBounds[1].Latitude = value; }
		double MaximumLongitude { get => mcolBounds[1].Longitude; set => mcolBounds[1].Longitude = value; }
		public Bounds(GeoLocation lower, GeoLocation upper)
		{
			mcolBounds[0] = lower;
			mcolBounds[1] = upper;
			Normalize();
		}
		public Bounds(double latitude1, double longitude1, double latitude2, double longitude2): this(new GeoLocation(latitude1, longitude1), new GeoLocation(latitude2, longitude2)){}

		private void Normalize()
		{
			double auxBajo, auxAlto;
			auxBajo = mcolBounds[0].Latitude;
			auxAlto = mcolBounds[1].Latitude;
			if(auxBajo>auxAlto)
			{
				mcolBounds[0].Latitude = auxAlto;
				mcolBounds[1].Latitude = auxBajo;
			}
			auxBajo = mcolBounds[0].Longitude;
			auxAlto = mcolBounds[1].Longitude;
			if (auxBajo > auxAlto)
			{
				mcolBounds[0].Longitude = auxAlto;
				mcolBounds[1].Longitude = auxBajo;
			}
		}
	}
}
