using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using TimeNet2026.Auxiliar;
using TimeNet2026.Storage;

namespace TimeNet2026.Topo
{
	public class RefPunctual : Punctual
	{																 
		internal GeoLocation point { get; set; }//Ubicación geográfica del punto
		internal RefPunctual(double latitude, double longitude)
		{
			point = new GeoLocation(latitude, longitude);
			pk = -1;
		}
		internal RefPunctual(XmlNode root)
		{
			double auxLatitude, auxLongitude;
			auxLatitude = XMLUtil.DoubleParam(root, "x");
			auxLongitude = XMLUtil.DoubleParam(root, "y");
			point = new GeoLocation(auxLatitude, auxLongitude);
			pk = -1;
		}
		internal static new List<OnyxField> Descriptor()
		{
			List<OnyxField> salida = Lineal.Descriptor();
			salida.Add(new OnyxField("latitude", "REAL"));
			salida.Add(new OnyxField("longitude", "REAL"));
			return salida;
		}
	}
}
