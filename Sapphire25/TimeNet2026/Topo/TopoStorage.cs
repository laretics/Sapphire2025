using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using TimeNet2026.Timed;
namespace TimeNet2026.Topo
{
	internal class TopoStorage
	{
		internal Header Header { get; set; } //Encabezado.
		internal Dictionary<string, Axis> mcolAxis; //Colección de ejes	
		internal Dictionary<string, Asimilation> mcolAsimilations; //Colección de asimilaciones.
		internal TopoStorage()
		{
			Header = new Header();
8x		mcolAsimilations = new Dictionary<string, Asimilation>();
			mcolAxis = new Dictionary<string, Axis>();
		}
		internal Axis? axisByStation(Station rhs)
		{
			foreach (Axis eje in mcolAxis.Values)
				if (eje.contains(rhs)) return eje;
			return null;
		}
		internal void deserialize(XmlNode root)
		{
			foreach(XmlNode hijo in root.ChildNodes)
			{
				switch (hijo.Name)
				{
					case "info": //Cabecera de información.
						this.Header.deserialize(hijo);
						break;
					case "topo": //Ejes
						Axis nuevo = new Axis(hijo);
						if (mcolAxis.ContainsKey(nuevo.id))
							mcolAxis.Remove(nuevo.id);
						mcolAxis.Add(nuevo.id, nuevo);
						break;
					case "asimilation": //Asimilaciones
						break;
				}
			}

		}
	}
}
