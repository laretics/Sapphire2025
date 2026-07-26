using System;

namespace Diamond.Topo
{
	/// <summary>
	/// Utilidades geodésicas locales (aproximación equirectangular / haversine en metros).
	/// Suficiente para polilíneas ferroviarias de escala regional.
	/// </summary>
	internal static class GeoMath
	{
		private const double EarthRadiusMeters = 6371008.8;
		private const double MetersPerDegreeLat = 111320.0;

		public static double DegreesToRadians(double degrees)
		{
			return degrees * (Math.PI / 180.0);
		}

		public static double RadiansToDegrees(double radians)
		{
			return radians * (180.0 / Math.PI);
		}

		public static double MetersPerDegreeLon(double latitudeDegrees)
		{
			double cosLat = Math.Cos(DegreesToRadians(latitudeDegrees));
			if (cosLat < 1e-12)
			{
				cosLat = 1e-12;
			}

			return MetersPerDegreeLat * cosLat;
		}

		/// <summary>
		/// Distancia haversine en metros entre dos WGS84.
		/// </summary>
		public static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
		{
			double phi1 = DegreesToRadians(lat1);
			double phi2 = DegreesToRadians(lat2);
			double dPhi = DegreesToRadians(lat2 - lat1);
			double dLambda = DegreesToRadians(lon2 - lon1);

			double sinDPhi = Math.Sin(dPhi * 0.5);
			double sinDLambda = Math.Sin(dLambda * 0.5);
			double a = sinDPhi * sinDPhi + Math.Cos(phi1) * Math.Cos(phi2) * sinDLambda * sinDLambda;
			double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(Math.Max(0.0, 1.0 - a)));
			return EarthRadiusMeters * c;
		}

		/// <summary>
		/// Convierte un desplazamiento en metros (este, norte) a delta lat/lon alrededor de un origen.
		/// </summary>
		public static void MetersToLatLonDelta(
			double originLat,
			double eastMeters,
			double northMeters,
			out double deltaLat,
			out double deltaLon)
		{
			deltaLat = northMeters / MetersPerDegreeLat;
			deltaLon = eastMeters / MetersPerDegreeLon(originLat);
		}

		/// <summary>
		/// Proyecta un punto WGS84 a coordenadas locales en metros (este, norte) respecto a un origen.
		/// </summary>
		public static void ToLocalMeters(
			double originLat,
			double originLon,
			double lat,
			double lon,
			out double eastMeters,
			out double northMeters)
		{
			northMeters = (lat - originLat) * MetersPerDegreeLat;
			eastMeters = (lon - originLon) * MetersPerDegreeLon(originLat);
		}

		/// <summary>
		/// Distancia mínima en metros de un punto a un segmento geodésico (aprox. plano local).
		/// Devuelve también el parámetro t clampado en [0,1] y el punto proyectado.
		/// </summary>
		public static double PointToSegmentMeters(
			double pointLat,
			double pointLon,
			double lat1,
			double lon1,
			double lat2,
			double lon2,
			out double t,
			out double projLat,
			out double projLon)
		{
			double originLat = pointLat;
			double originLon = pointLon;

			double pE;
			double pN;
			ToLocalMeters(originLat, originLon, pointLat, pointLon, out pE, out pN);

			double aE;
			double aN;
			ToLocalMeters(originLat, originLon, lat1, lon1, out aE, out aN);

			double bE;
			double bN;
			ToLocalMeters(originLat, originLon, lat2, lon2, out bE, out bN);

			double abE = bE - aE;
			double abN = bN - aN;
			double apE = pE - aE;
			double apN = pN - aN;

			double abLen2 = abE * abE + abN * abN;
			if (abLen2 < 1e-12)
			{
				t = 0.0;
				projLat = lat1;
				projLon = lon1;
				return Math.Sqrt(apE * apE + apN * apN);
			}

			t = (apE * abE + apN * abN) / abLen2;
			if (t < 0.0)
			{
				t = 0.0;
			}
			else if (t > 1.0)
			{
				t = 1.0;
			}

			double qE = aE + t * abE;
			double qN = aN + t * abN;
			double dE = pE - qE;
			double dN = pN - qN;

			// q en local respecto al punto de consulta (origen del plano).
			projLat = originLat + qN / MetersPerDegreeLat;
			projLon = originLon + qE / MetersPerDegreeLon(originLat);

			return Math.Sqrt(dE * dE + dN * dN);
		}

		/// <summary>
		/// Cota inferior de distancia en metros de un punto a un rectángulo lat/lon.
		/// </summary>
		public static double PointToBoundingBoxMeters(
			double pointLat,
			double pointLon,
			double minLat,
			double maxLat,
			double minLon,
			double maxLon)
		{
			double clampedLat = pointLat;
			if (clampedLat < minLat)
			{
				clampedLat = minLat;
			}
			else if (clampedLat > maxLat)
			{
				clampedLat = maxLat;
			}

			double clampedLon = pointLon;
			if (clampedLon < minLon)
			{
				clampedLon = minLon;
			}
			else if (clampedLon > maxLon)
			{
				clampedLon = maxLon;
			}

			if (clampedLat == pointLat && clampedLon == pointLon)
			{
				return 0.0;
			}

			return HaversineMeters(pointLat, pointLon, clampedLat, clampedLon);
		}
	}
}
