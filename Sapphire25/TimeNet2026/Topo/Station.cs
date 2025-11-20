using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using TimeNet2026.Auxiliar;

namespace TimeNet2026.Topo
{
	internal class Station : RefPunctual
	{
		internal string id { get; private set; }
		internal string name { get; private set; }
		internal string shortName { get; private set; }
		internal Axis axis { get; private set; }

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
	}
}
