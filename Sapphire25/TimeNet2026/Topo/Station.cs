using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using TimeNet2026.Auxiliar;
using TimeNet2026.Storage;

namespace TimeNet2026.Topo
{
	public class Station : RefPunctual, Entity
	{
		public string id { get; private set; }
		public string name { get; private set; }
		public string shortName { get; private set; }
		internal Axis axis { get; private set; }
		string Entity.name { get => this.name; set => this.name = value; }
		string Entity.comment { get => this.name; set => this.name = value; }
		string[] Entity.color { get =>	new string[2]; set => throw new NotImplementedException(); }

		public Station(string id, string name, string shortName, Axis axis, double latitude, double longitude) : base(latitude, longitude)
		{
			this.id = id;
			this.name = name;
			this.shortName = shortName;
			this.axis = axis;
		}
		public Station(XmlNode root, Axis axis):base(root)
		{
			this.id = XMLUtil.StringParam(root, "id");
			this.name = XMLUtil.StringParam(root, "name");
			this.shortName = XMLUtil.StringParam(root, "avr");
			this.pk = XMLUtil.LongParam(root, "pk");
			this.axis = axis;
		}
		public bool isStation()
		{
			//Es una estación si el código Id comienza por mayúsculas.
			string primeraLetra = shortName.Substring(0, 1);
			return primeraLetra.Equals(primeraLetra.ToUpper());
		}
		internal static new List<OnyxField> Descriptor()
		{
			List<OnyxField> salida = RefPunctual.Descriptor();
			salida.Add(new OnyxField("id", "STRING", true, true, false));
			salida.Add(new OnyxField("name", "STRING"));
			salida.Add(new OnyxField("shortName", "STRING"));
			return salida;
		}
	}
}
