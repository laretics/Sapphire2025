using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using TimeNet2026.Topo;

namespace TimeNet2026.Timed
{
	public class Plan : Entity
	//Plan de explotación
	{
		public Header Header { get; set; } //Encabezado del plan de explotación		
		public string[] mvarColor { get; set; }
		public string Color { get => mvarColor[0]; }
		public IEnumerable<Circulation> Circulations { get => mcolCirculations.Values; }
		public IEnumerable<Schedule> Schedules { get => mcolSchedules.Values; }
		internal Dictionary<string, Circulation> mcolCirculations;
		internal Dictionary<string, Schedule> mcolSchedules;
		string[] Entity.color { get => mvarColor; set => mvarColor = value; }
		string Entity.name { get => Header.Name; set => Header.Name = value; }
		string Entity.comment { get => Header.Comment; set => Header.Comment = value; }
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
		internal Circulation proximalCirculationByStation(Station station, TimeSpan time)
		{
			Circulation candidate = null;
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

		internal Schedule scheduleByCirculation(Circulation rhs)
		{
			foreach (Schedule auxSchedule in mcolSchedules.Values)
			{
				if (auxSchedule.containsCirculation(rhs)) return auxSchedule;
			}
			return null;
		}
		internal Schedule? currentSchedule { get; set; }

		internal Plan()
		{
			Header = new Header();
			mvarColor = new string[2];
			mcolCirculations = new Dictionary<string, Circulation>();
			mcolSchedules = new Dictionary<string, Schedule>();
		}
		internal Plan(XmlNode root):this()
		{
			foreach(XmlNode hijo in root.ChildNodes)
			{
				switch (hijo.Name)
				{
					case "info": //Cabecera del documento
						this.Header.deserialize(hijo);
						break;
				}
			}
		}

	}
}
