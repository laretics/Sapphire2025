using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using TimeNet2026.Auxiliar;
using TimeNet2026.Storage;

namespace TimeNet2026.Topo
{
	public class Station : RefPunctual, Entity
	{
		public string Id { get; internal set; }
		public string Name { get; internal set; }
		public string ShortName { get; internal set; }
		internal Axis axis { get; private set; }
		string Entity.name { get => this.Name; set => this.Name = value; }
		string Entity.comment { get => this.Name; set => this.Name = value; }
		string[] Entity.color { get =>	new string[2]; set => throw new NotImplementedException(); }

		public Station(Axis axis)
		{
			this.axis = axis;
		}
		public Station(string id, string name, string shortName, Axis axis, double latitude, double longitude) : base(latitude, longitude)
		{
			Id = id;
			Name = name;
			ShortName = shortName;
			this.axis = axis;
		}
		internal override string XNode()
		{
			return string.Format("<point x=\"{0}\" y=\"{1}\" name=\"{2}\" avr=\"{3}\" pk=\"{4}\" id=\"{5}\" />", 
				point.Latitude, 
				point.Longitude,
				Name,
				ShortName,
				pk,
				Id);
		}
		public bool isStation()
		{
			//Es una estación si el código Id comienza por mayúsculas.
			string primeraLetra = ShortName.Substring(0, 1);
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
