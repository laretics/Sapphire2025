using System.Diagnostics;
using System.Xml.Linq;
using TimeNet2026.Auxiliar;
using TimeNet2026.Storage;

namespace TimeNet2026.Topo
{
	public class RefPunctual : Punctual
	{																 
		internal GeoLocation point { get; set; }//Ubicación geográfica del punto
		internal RefPunctual(double latitude, double longitude) : this()
		{
			point = new GeoLocation(latitude, longitude);
		}
		internal RefPunctual()
		{
			pk = -1;
		}
		internal virtual string XNode()
		{
			return string.Format("<point x=\"{0}\" y=\"{1}\" />",  point.Latitude,point.Longitude);
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
