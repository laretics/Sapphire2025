using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimeNet2026.Topo
{
	/// <summary>
	/// Clase que almacena una polilínea en coordenadas geográficas
	/// </summary>
	internal class GeoPolyline
	{
		internal List<GeoLocation> mcolPoints;
		internal GeoPolyline()
		{
			mcolPoints = new List<GeoLocation>();
		}
		public void add(GeoLocation point)
		{
			mcolPoints.Add(point);
		}
		public void add(GeoPolyline polyline)
		{
			foreach (GeoLocation point in polyline.mcolPoints)
				mcolPoints.Add(point);
		}
	}
}
