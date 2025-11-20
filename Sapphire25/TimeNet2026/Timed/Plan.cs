using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimeNet2026.Topo;

namespace TimeNet2026.Timed
{
	internal class Plan : Entity
	//Plan de explotación
	{
		internal string id { get; set; }
		internal string mvarName { get; set; }
		internal string mvarComment { get; set; }
		internal string[] mvarColor { get; set; }
		internal Dictionary<string, Circulation> mcolCirculations;
		internal Dictionary<string, Schedule> mcolSchedules;
		string Entity.name { get => mvarName; set => mvarName = value; }
		string Entity.comment { get => mvarComment; set => mvarComment = value; }
		string[] Entity.color { get => mvarColor; set => mvarColor = value; }
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

		internal Circulation currentCirculation { get; set; }
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
		internal Schedule currentSchedule { get; set; }

		internal Plan()
		{
			mcolCirculations = new Dictionary<string, Circulation>();
			mcolSchedules = new Dictionary<string, Schedule>();
		}

	}
}
