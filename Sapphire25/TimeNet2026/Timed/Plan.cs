using Microsoft.EntityFrameworkCore.ValueGeneration.Internal;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using TimeNet2026.Auxiliar;
using TimeNet2026.Topo;

namespace TimeNet2026.Timed
{
	public class Plan : Entity
	//Plan de explotación
	{		
		internal string mvarId; //Identificador del plan
		internal string mvarName; //Nombre del plan
		internal string mvarComment; //Comentarios del plan
		public string[] mvarColor { get; set; }
		public string Color { get => mvarColor[0]; }
		public string Name { get => mvarName; }
        public string Id { get => mvarId; } //Identificador del plan
		public TopoStorage Parent { get; private set; }
        public IEnumerable<Circulation> Circulations { get => mcolCirculations.Values; }
		public IEnumerable<Schedule> Schedules { get => mcolSchedules.Values; }
		internal Dictionary<string, Circulation> mcolCirculations;
		internal Dictionary<string, Schedule> mcolSchedules;
		string[] Entity.color { get => mvarColor; set => mvarColor = value; }
		string Entity.name { get => mvarName; set => mvarName = value; }
		string Entity.comment { get => mvarComment; set => mvarComment = value; }
		internal Guid TopoId { get; set; } //Id de compatibilidad con la topología.
		internal List<Circulation> nextCirculationsByStation(Station station, TimeSpan time)
		{
			List<Circulation> salida = new List<Circulation>();
			//Obtiene el próximo tren en partir a partir de esta hora...
			foreach (Circulation auxCircula in mcolCirculations.Values)
			{
				TimeSpan auxTime = auxCircula.departureFrom(station);
				if ((auxTime < TimeSpan.MaxValue) && (auxTime >= time))
				{
					auxCircula.cacheDeparture = auxTime;
					salida.Add(auxCircula);
				}
			}
			salida.Sort();
			return salida;
		}
		internal Circulation? proximalCirculationByStation(Station station, TimeSpan time)
		{
			Circulation? candidate = null;
			double nearestCandidate = double.MaxValue;
			double auxNearest = double.MaxValue;
			foreach (Circulation auxCircula in mcolCirculations.Values)
			{
				TimeSpan auxTime = auxCircula.departureFrom(station);
				auxNearest = Math.Abs(time.TotalMilliseconds - auxTime.TotalMilliseconds);
				if (auxNearest < nearestCandidate)
				{
					nearestCandidate = auxNearest;
					candidate = auxCircula;
				}
			}
			return candidate;
		}

		internal Circulation? currentCirculation { get; set; }
		internal void setCirculation(string rhs)
		{
			if (mcolCirculations.ContainsKey(rhs)) currentCirculation = mcolCirculations[rhs];
		}

		internal Schedule? scheduleByCirculation(Circulation rhs)
		{
			foreach (Schedule auxSchedule in mcolSchedules.Values)
			{
				if (auxSchedule.containsCirculation(rhs)) return auxSchedule;
			}
			return null;
		}
		internal Schedule? currentSchedule { get; set; }

		internal Plan(TopoStorage parent)
		{
			this.Parent = parent;
			mvarId = string.Empty;
			mvarName = string.Empty;
			mvarComment = string.Empty;
			mvarColor = new string[2];
			mcolCirculations = new Dictionary<string, Circulation>();
			mcolSchedules = new Dictionary<string, Schedule>();
		}
		internal Plan(XmlNode root, TopoStorage topoStorage):this(topoStorage)
		{
			mvarId = XMLUtil.StringParam(root, "id");
			mvarName = XMLUtil.StringParam(root, "name");
			mvarComment = XMLUtil.StringParam(root, "comment");
			foreach(XmlNode hijo in root.ChildNodes)
			{
				switch (hijo.Name)
				{
					case "circulations": //Circulaciones definidas en el plan
						deserializeCirculations(hijo,Parent);
						break;
					case "schedules": //Horarios definidos en el plan
						deserializeSchedules(hijo);
						break;
				}
			}
		}
		internal void deserializeCirculations(XmlNode root, TopoStorage topoStorage)
		{
			foreach (XmlNode hijo in root.ChildNodes)
			{
				Circulation nueva = new Circulation(hijo, topoStorage);
				mcolCirculations.Add(nueva.name, nueva);
			}
		}
		internal void deserializeSchedules(XmlNode root)
		{
			foreach (XmlNode hijo in root.ChildNodes)
			{
				if(hijo.Name=="active")
				{
					foreach (XmlNode nieto in hijo.ChildNodes)
					{
						if (nieto.Name == "ws")
						{
							Schedule nuevoTurno = new Schedule();
							nuevoTurno.deserialize(hijo, this);
							string[] nombres = nuevoTurno.name.Split(',');
							if(nombres.Length > 1)
							{
								//Varios nombres
								foreach (string nombre in nombres)
								{
									Schedule turnoAux = new Schedule();
									turnoAux.deserialize(hijo, this);
									turnoAux.name = nombre.Trim();
									mcolSchedules.Add(turnoAux.name, turnoAux);
								}
							}
						}
					}

				}
			}
		}
	}
}
