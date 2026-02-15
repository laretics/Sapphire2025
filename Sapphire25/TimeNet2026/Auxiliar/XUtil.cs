using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using TimeNet2026.Topo;

namespace TimeNet2026.Auxiliar
{
	internal class XUtil
	{
		#region Lectura
		internal static string StringParam(XNode node, string paramId, string defaultValue="")
		{
			if (node is XElement element)
			{
				return element.Attribute(paramId)?.Value ?? defaultValue;
			}
			return defaultValue;				
		}
		internal static bool BoolParam(XNode node, string paramId, bool defaultValue=false)
		{
			if (node is XElement element)
			{
				string salida = StringParam(node, paramId, "XX");
				if (salida != "XX")
					return salida.ToUpper().Contains("T");
			}
			return defaultValue;
		}
		internal static byte ByteParam(XNode node, string paramId, byte defaultValue = 255)
		{
			if (node is XElement element && element.Attribute(paramId)?.Value is string value)
				return byte.TryParse(value, out var result) ? result : defaultValue;

			return defaultValue;
		}
		internal static int IntParam(XNode node, string paramId, int defaultValue = -1)
		{
			if (node is XElement element && element.Attribute(paramId)?.Value is string value)
				return int.TryParse(value, out int result) ? result : defaultValue;

			return defaultValue;
		}
		internal static long LongParam(XNode node, string paramId, long defaultValue = -1)
		{
			if (node is XElement element && element.Attribute(paramId)?.Value is string value)
				return long.TryParse(value, out long result) ? result : defaultValue;

			return defaultValue;
		}
		internal static double DoubleParam(XNode node, string paramId, double defaultValue = double.NaN)
		{
			if (node is XElement element && element.Attribute(paramId)?.Value is string value)
				return double.TryParse(value, out double result) ? result : defaultValue;

			return defaultValue;
		}
		internal static Guid GuidParam(XNode node, string paramId)
		{
			if (node is XElement element && element.Attribute(paramId)?.Value is string value)
				return Guid.TryParse(value, out Guid result) ? result : Guid.Empty;

			return Guid.Empty;
		}
		internal static DateTime DateTimeParam(XNode node, string paramId)
		{
			if (node is XElement element && element.Attribute(paramId)?.Value is string value)
				return DateTime.TryParse(value, out DateTime result) ? result : DateTime.MinValue;

			return DateTime.MinValue;
		}
		internal static TimeSpan TimeSpanParam(XNode node, string paramId)
		{
			if (node is XElement element && element.Attribute(paramId)?.Value is string value)
				return TimeSpan.TryParse(value, out TimeSpan result) ? result : new TimeSpan(0);

			return new TimeSpan(0);
		}
		internal static GeoLocation GeoLocationParam(XNode node)
		{
			return new GeoLocation(DoubleParam(node, "x", 0), DoubleParam(node, "y", 0));
		}
		#endregion Lectura
		#region Escritura


		#endregion Escritura
	}
}
