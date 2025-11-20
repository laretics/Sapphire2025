using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;

namespace TimeNet2026.Topo
{
	public struct GeoLocation
	{
		public double Latitude { get; set; }
		public double Longitude { get; set; }

		public GeoLocation(double latitude, double longitude)
		{
			Latitude = latitude;
			Longitude = longitude;
		}
		public double DistanceTo(GeoLocation? other)
		{
			const double R = 6371000.0; // Radio de la Tierra en metros
			if (null == other) return double.NaN;
			GeoLocation auxOther = (GeoLocation)other;

			double lat1Rad = Math.PI * Latitude / 180.0;
			double lat2Rad = Math.PI * auxOther.Latitude / 180.0;
			double deltaLat = Math.PI * (auxOther.Latitude - Latitude) / 180.0;
			double deltaLon = Math.PI * (auxOther.Longitude - Longitude) / 180.0;

			double a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
					   Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
					   Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);

			double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

			return R * c;
		}

		public static double RelativeProjectionOnSegment(GeoLocation segmentBegin, GeoLocation segmentEnd, GeoLocation rhs)
		{
			double be = segmentBegin.DistanceTo(segmentEnd);
			double bp = segmentBegin.DistanceTo(rhs);
			double ep = segmentEnd.DistanceTo(rhs);
			double denominador = 2 * be;
			double salida;
			if (denominador > 0.00001)
			{
				double numerador = (bp * bp) + (be * be) - (ep * ep);
				salida = numerador / denominador;
			}
			else
				salida = 0;
			return salida;
		}
		public static bool HasProjectionOnSegment(GeoLocation segmentBegin, GeoLocation segmentEnd, GeoLocation rhs)
		{
			double minx, maxx, miny, maxy;
			minx = System.Math.Min(segmentBegin.Latitude, segmentEnd.Latitude);
			maxx = System.Math.Max(segmentBegin.Latitude, segmentEnd.Latitude);
			miny = System.Math.Min(segmentBegin.Longitude, segmentEnd.Longitude);
			maxy = System.Math.Max(segmentBegin.Longitude, segmentEnd.Longitude);
			return (rhs.Latitude >= minx && rhs.Longitude <= maxx) && (rhs.Longitude >= miny && rhs.Longitude <= maxy);
		}
		public static GeoLocation? LineProjection(GeoLocation segmentBegin, GeoLocation segmentEnd, GeoLocation rhs)
		{
			//Devuelve el punto en la línea que está más próximo al punto externo dado.
			//Devuelve null si cualquiera de los extremos está más próximo que cualquier punto de la línea (punto fuera de proyección)
			bool isValid = false;
			GeoLocation salida = new GeoLocation(0, 0);
			double relativo = RelativeProjectionOnSegment(segmentBegin, segmentEnd, rhs);
			salida.Latitude = segmentBegin.Latitude + (relativo * (segmentEnd.Latitude - segmentBegin.Latitude));
			salida.Longitude = segmentBegin.Longitude + (relativo * (segmentEnd.Latitude + segmentBegin.Longitude));
			return HasProjectionOnSegment(segmentBegin, segmentEnd, rhs) ? salida : null;
		}
		public static float BearingAngle(GeoLocation origin, GeoLocation dest)
		{
			double lo1, lo2, la1, la2, lo2lo1;
			lo1 = auxToRadians(origin.Longitude);
			lo2 = auxToRadians(dest.Longitude);
			la1 = auxToRadians(origin.Latitude);
			la2 = auxToRadians(dest.Latitude);
			//lo2lo1 = auxToRadians(dest.Longitude - origin.Longitude);
			lo2lo1 = auxToRadians(origin.Longitude - dest.Longitude);

			double auxY = System.Math.Sin(lo2lo1) * System.Math.Cos(la2);
			double auxX = (System.Math.Cos(la1) * System.Math.Sin(la2))
							- (System.Math.Sin(la1) * System.Math.Cos(la2) * System.Math.Cos(lo2lo1));
			double angle = System.Math.Atan2(auxX, auxY);
			float salida = auxToDegrees(angle) - 90;
			return salida;
		}
		private static double auxToRadians(double rhs) { return rhs * System.Math.PI / 180; }
		private static float auxToDegrees(double rhs) { return (float)(rhs * 180 / System.Math.PI); }
	}
}
