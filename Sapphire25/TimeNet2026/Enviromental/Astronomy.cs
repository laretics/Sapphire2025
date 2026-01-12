using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimeNet2026.Enviromental
{
	public struct SunTimes
	{
		public double sunrise { get; set; }
		public double sunset { get; set; }
	}
	public class Astronomy
	{
		private static double ToRadians(double degrees) => degrees * Math.PI / 180;
		private static double ToDegrees(double radians) => radians * 180 / Math.PI;

		public static (TimeSpan sunrise, TimeSpan sunset) CalculateSunriseSunset(DateTime date, double latitude, double longitude)
		{
			// Approximate calculation for sunrise and sunset
			// Julian day calculation
			int year = date.Year;
			int month = date.Month;
			int day = date.Day;
			if (month <= 2)
			{
				year--;
				month += 12;
			}
			double a = Math.Floor(year / 100.0);
			double b = 2 - a + Math.Floor(a / 4);
			double jd = Math.Floor(365.25 * (year + 4716)) + Math.Floor(30.6001 * (month + 1)) + day + b - 1524.5;

			// Solar calculations
			double n = jd - 2451545.0;
			double meanAnomaly = (357.5291 + 0.98560028 * n) % 360;
			double center = 1.9148 * Math.Sin(ToRadians(meanAnomaly)) + 0.0200 * Math.Sin(ToRadians(2 * meanAnomaly)) + 0.0003 * Math.Sin(ToRadians(3 * meanAnomaly));
			double eclipticLongitude = (280.46646 + 0.9856474 * n) % 360 + center;
			double transit = jd + 0.0053 * Math.Sin(ToRadians(meanAnomaly)) - 0.0069 * Math.Sin(ToRadians(2 * eclipticLongitude));
			double declination = Math.Asin(Math.Sin(ToRadians(eclipticLongitude)) * Math.Sin(ToRadians(23.4397))) * 180 / Math.PI;

			// Hour angle
			double hourAngle = Math.Acos((Math.Sin(ToRadians(-0.83)) - Math.Sin(ToRadians(latitude)) * Math.Sin(ToRadians(declination))) / (Math.Cos(ToRadians(latitude)) * Math.Cos(ToRadians(declination)))) * 180 / Math.PI;

			// Adjust for longitude
			double longitudeAdjustment = longitude / 15.0;
			double sunriseJd = transit - hourAngle / 360 - longitudeAdjustment / 24;
			double sunsetJd = transit + hourAngle / 360 - longitudeAdjustment / 24;

			// Convert to TimeSpan
			double sunriseFraction = sunriseJd - Math.Floor(sunriseJd);
			double sunsetFraction = sunsetJd - Math.Floor(sunsetJd);
			TimeSpan sunrise = TimeSpan.FromDays(sunriseFraction);
			TimeSpan sunset = TimeSpan.FromDays(sunsetFraction);

			return (sunrise, sunset);
		}
	}
}
