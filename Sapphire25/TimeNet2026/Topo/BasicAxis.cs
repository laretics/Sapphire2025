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
	/// <summary>
	/// El eje completo es necesario en Ónice para tener la orientación, pero no en gestión de horarios.
	/// Esta versión simplificada es suficiente para Zafiro.
	/// </summary>
	public class BasicAxis:Lineal, Entity
	{
		protected string mvarName { get; set; }
		protected string mvarComment { get; set; }
		internal string[] mvarColor { get; set; }
		string Entity.name { get => mvarName; set => mvarName = value; }
		string Entity.comment { get => mvarComment; set => mvarComment = value; }
		String[] Entity.color { get => mvarColor; set => mvarColor = value; }
		public string id { get; set; }
		public TopoAxis? Topology { get; set; }
		internal List<Station> mcolStations; // Estaciones en el eje
		public List<Station> Stations { get => mcolStations; }
		internal static new List<OnyxField> Descriptor()
		{
			List<OnyxField> salida = Lineal.Descriptor();
			salida.Add(new OnyxField("id", "STRING", true, true, false));
			salida.Add(new OnyxField("name", "STRING"));
			salida.Add(new OnyxField("comment", "STRING"));
			salida.Add(new OnyxField("color0", "STRING"));
			salida.Add(new OnyxField("color1", "STRING"));
			return salida;
		}
		internal Station? nearestStation(long rhs) //Estación más cercana a este PK
		{
			if (null == mcolStations) return null; //Colección vacía.
			long candidateDistance = long.MaxValue;
			Station? candidate = null;
			foreach (Station auxStation in mcolStations)
			{
				long auxDistance = auxStation.distanceFrom(rhs);
				if (auxDistance < candidateDistance)
				{
					candidateDistance = auxDistance;
					candidate = auxStation;
				}
			}
			return candidate;
		}
		internal List<Station> nearestStations(long rhs) //Las dos estaciones más cercanas a este PK
		{
			List<Station> salida = new List<Station>();
			Station? anterior = null;
			if (null != mcolStations)
			{
				foreach (Station auxStation in mcolStations)
				{
					if (auxStation.isNear(rhs))
					{ //Estamos en esta estación, así que devolveremos sólo esa estación.
						salida.Add(auxStation);
						return salida;
					}
					else
					{
						if (anterior != null)
						{
							if ((anterior.pk < rhs) && (auxStation.pk > rhs))
							{
								salida.Add(anterior);
								salida.Add(auxStation);
								return salida;
							}
						}
						anterior = auxStation;
					}
				}
			}
			//Llegados a este punto nos hemos pasado el eje completo. Devolvemos una lista vacía.
			return salida;
		}
		internal Station? stationById(string id)
		{
			if (null == mcolStations) return null;
			foreach (Station auxStation in mcolStations)
				if (id.Equals(auxStation.id)) return auxStation;
			return null;
		}
		internal int mvarMaxSpeed;
		internal int maxSpeed { get => mvarMaxSpeed; }
		internal override bool contains(Punctual rhs)
		{
			if (rhs.GetType() == typeof(Station))
			{
				if (null != mcolStations)
				{
					foreach (Station auxStation in mcolStations)
						if (auxStation == rhs) return true;
				}
				return false;
			}
			return base.contains(rhs);
		}
		internal string neighbourhod(long rhs) //Devuelve información sobre la ubicación en lenguaje humano
		{
			List<Station> auxStations = nearestStations(rhs);
			if (auxStations.Count == 1)
			{
				return string.Format("In {0}", auxStations[0].name);
			}
			else if (auxStations.Count == 2)
			{
				int mostNear;
				mostNear = (auxStations[0].distanceFrom(rhs) < auxStations[1].distanceFrom(rhs)) ? 0 : 1;
				return string.Format("Between {0} and {1}. (At {2}m from {3})",
					auxStations[0].name,
					auxStations[1].name,
					auxStations[mostNear].distanceFrom(rhs),
					auxStations[mostNear].name);
			}
			return "";
		}
		internal override string XNode()
		{
			StringBuilder salida = new StringBuilder();
			salida.AppendFormat("\t<axis id=\"{0}\" name=\"{1}\" comment=\"{2}\" vmax=\"{3}\" color=\"{4}\" darkcolor=\"{5}\" >\n",
				id,
				mvarName,
				mvarComment,
				mvarMaxSpeed,
				mvarColor[0],
				mvarColor[1]
				);
			salida.AppendLine("\t\t<poly>");
			if(null!=Topology)
			{
				foreach (RefPunctual auxPunto in Topology.mcolPoints)
					salida.AppendLine("\t\t\t" + auxPunto.XNode());
			}
			salida.AppendLine("\t\t</poly>");
			salida.AppendLine("\t\t<limit>\n");
			if(null!=Topology)
			{
				foreach (SpeedLimit auxLimit in Topology.mcolSpeedLimits)
					salida.AppendLine("\t\t\t" + auxLimit.XNode());
			}
			salida.AppendLine("\t\t</limit>\n");
			salida.AppendLine("\t\t<signal>\n");
			if(null!=Topology)
			{
				///Implementar aquí las señales que contiene el eje.
			}
			salida.AppendLine("\t\t</signal>\n");
			salida.AppendLine("\t</axis>");
			return salida.ToString();
		}
		public BasicAxis()
		{
			id = "Unnamed";
			mvarName = "Unnamed";
			mvarComment = string.Empty;
			mvarMaxSpeed = 100; //Valor por defecto a remplazar.
			mcolStations = new List<Station>();
			mvarColor = new string[2];
		}
		protected void deserializeXHeader(XNode root)
		{
			this.id = XUtil.StringParam(root, "id");
			this.mvarName = XUtil.StringParam(root, "name");
			this.mvarComment = XUtil.StringParam(root, "comment");
			this.mvarMaxSpeed = XUtil.IntParam(root, "vmax");
			this.mvarColor[0] = XUtil.StringParam(root, "color");
			this.mvarColor[1] = XUtil.StringParam(root, "darkcolor");
		}
		protected void deserializeXStations(XElement root)
		{

		}
	}
}
