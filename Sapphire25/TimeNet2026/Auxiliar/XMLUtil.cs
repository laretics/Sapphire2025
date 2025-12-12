using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using TimeNet2026.Topo;

namespace TimeNet2026.Auxiliar
{
	internal static class XMLUtil
	{
		internal static string StringParam(XmlNode node, string paramId, string defaultValue ="")
		{
			if (null == node.Attributes) return defaultValue;
			XmlAttribute? auxAttribute = node.Attributes[paramId];
			if (null == auxAttribute) return defaultValue;
			return auxAttribute.Value;
		}
		internal static byte ByteParam(XmlNode node, string paramId, byte defaultValue = 255)
		{
			string entrada = StringParam(node, paramId, "nan");
			if ("nan" == entrada) return defaultValue;
			byte salida = defaultValue;
			byte.TryParse(entrada, out salida);
			return salida;
		}
		internal static int IntParam(XmlNode node, string paramId, int defaultValue=-1)
		{
			string entrada = StringParam(node, paramId, "nan");
			if ("nan" == entrada) return defaultValue;
			int salida = defaultValue;
			int.TryParse(entrada, out salida);
			return salida;
		}
		internal static long LongParam(XmlNode node, string paramId, long defaultValue = -1)
		{
			string entrada = StringParam(node, paramId, "nan");
			if ("nan" == entrada) return defaultValue;
			long salida = defaultValue;
			long.TryParse(entrada, out salida);
			return salida;
		}
		internal static Guid GuidParam(XmlNode node, string paramId)
		{
			string entrada = StringParam(node, paramId, "nan");
			if ("nan" == entrada) return Guid.Empty;
			Guid salida = Guid.Empty;
			Guid.TryParse(entrada, out salida);
			return salida;
		}
		internal static TimeSpan TimeSpanParam(XmlNode node, string paramId)
		{
			string entrada = StringParam(node, paramId, "nan");
			if ("nan" == entrada) return new TimeSpan(0);
			TimeSpan salida = new TimeSpan(0);
			TimeSpan.TryParse(entrada, out salida);
			return salida;
		}
		internal static DateTime DateTimeParam(XmlNode node, string paramId)
		{
			string entrada = StringParam(node, paramId, "nan");
			if ("nan" == entrada) return DateTime.MinValue;
			DateTime salida = DateTime.MinValue;
			DateTime.TryParse(entrada, out salida);
			return salida;
		}
		internal static double DoubleParam(XmlNode node, string paramId, double defaultValue=double.NaN)
		{
			string entrada = StringParam(node, paramId, "nan");
			if ("nan" == entrada) return defaultValue;
			double salida = defaultValue;
			entrada = entrada.Replace(".", ",");
			double.TryParse(entrada, out salida);
			return salida;
		}
		internal static GeoLocation GeoLocationParam(XmlNode node)
		{
			GeoLocation salida = new GeoLocation();
			salida.Latitude = DoubleParam(node, "x", 0);
			salida.Longitude = DoubleParam(node, "y", 0);
			return salida;
		}
	}
}
