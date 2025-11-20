using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using TimeNet2026.Timed;
using TimeNet2026.Topo;

namespace TimeNet2026
{
	internal class OnyxStorage
	{
		internal List<TopoStorage> mcolTopoStorage; //Colección de topologías de distintos sitios
		internal Dictionary<string, Plan> mcolPlans; //Colección de planes de explotación.
		internal OnyxStorage()
		{

			mcolPlans = new Dictionary<string, Plan>();
		}


		internal void deserializeTopo(XmlNode root)
		{
			//Root es el nodo "layout"
			TopoStorage nuevo = new TopoStorage(root);
			mcolTopoStorage.Add(nuevo);
		}
		internal void deserializeRauta(XmlNode root)
		{

		}













	}
}
