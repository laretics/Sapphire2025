using System;
using System.Collections.Generic;
using System.ComponentModel.Design.Serialization;
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
	public class Axis:Lineal, Entity
	{
		internal string[] mvarColor { get; set; }
		public string Name { get; set; }
		public string Comment { get; set; }
		public int MaxSpeed { get; set; }
		string Entity.name { get => Name; set => Name = value; }
		string Entity.comment { get => Comment; set => Comment = value; }
		String[] Entity.color { get => mvarColor; set => mvarColor = value; }
		public string id { get; set; }
		public TopoAxis? Topology { get; set; }
		public List<Station> Stations { get; private set; }
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
			if (null == Stations) return null; //Colección vacía.
			long candidateDistance = long.MaxValue;
			Station? candidate = null;
			foreach (Station auxStation in Stations)
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
			if (null != Stations)
			{
				foreach (Station auxStation in Stations)
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
			if (null == Stations) return null;
			foreach (Station auxStation in Stations)
				if (id.Equals(auxStation.id)) return auxStation;
			return null;
		}
		
		internal override bool contains(Punctual rhs)
		{
			if (rhs.GetType() == typeof(Station))
			{
				if (null != Stations)
				{
					foreach (Station auxStation in Stations)
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

		internal Bounds getBounds()
		{
			List<RefPunctual> points = new List<RefPunctual>();
			foreach (Station auxEstacion in Stations)
				points.Add(auxEstacion);
			return TopoAxis.polyLineBounds(points);
		}
		
		public Axis()
		{
			id = "Unnamed";
			Name = "Unnamed";
			Comment = string.Empty;
			MaxSpeed = 100; //Valor por defecto a remplazar.
			Stations = new List<Station>();
			mvarColor = new string[2];
		}
	}
}
