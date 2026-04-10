using Microsoft.EntityFrameworkCore.ValueGeneration.Internal;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using TimeNet2026.Auxiliar;
using TimeNet2026.Topo;

namespace TimeNet2026.Timed
{
	public class Plan : Entity
	//Plan de explotación
	{
		public Weekday CurrentDay { get; set; } = Weekday.Monday;
		public string[] mvarColor { get; set; }
		public string Color { get => mvarColor[0]; }
		public string Name { get; set; }
		public string Comment { get; set; }
        public string Id { get; set; } //Identificador del plan
		public TopoStorage Parent { get; private set; }
		public int CirculationCount { get => CirculationsByDay.Count(); }
		public override string ToString()
		{
			return this.Name;
		}
		public IEnumerable<Schedule> AllSchedules { get => mcolSchedules; }
		public IEnumerable<Schedule> SchedulesByDay
		{
			get
			{
                foreach (Schedule auxSchedule in mcolSchedules)
                {
                    if (auxSchedule.weekdayMask.HasFlag(CurrentDay))
                        yield return auxSchedule;
                }
            }
		}
		public List<CirculationBlock> AllCirculationBlocks { get; private set; }
		public List<CirculationBlock> CirculationBlocksByDay
		{
			get
			{
				List<CirculationBlock> salida = new List<CirculationBlock>();
				foreach (CirculationBlock bloque in AllCirculationBlocks)					
				{
					if (bloque.weekdayMask.HasFlag(CurrentDay))
						salida.Add(bloque);
                }
				return salida;
			}
        }
        public IEnumerable<Circulation> CirculationsByDay
		{
			get
			{
				foreach (CirculationBlock bloque in AllCirculationBlocks)
				{
					foreach (Circulation circula in bloque.Circulations)
						yield return circula;
				}
			}
        }

        internal List<Schedule> mcolSchedules; //No puedo hacer un diccionario porque puede haber varios turnos con el mismo nombre en días diferentes.
		internal Schedule? Schedule(string name,Weekday dayOfWeek)
		{
			foreach (Schedule auxSchedule in mcolSchedules)
			{
				if ((auxSchedule.Name == name) && auxSchedule.weekdayMask.HasFlag(dayOfWeek)) return auxSchedule;
			}
			return null;
		}
		string[] Entity.color { get => mvarColor; set => mvarColor = value; }
		string Entity.name { get => Name; set => Name = value; }
		string Entity.comment { get => Comment; set => Comment = value; }
		internal Guid TopoId { get; set; } //Id de compatibilidad con la topología.
		internal List<Circulation> nextCirculationsByStation(Station station, TimeSpan time)
		{
			List<Circulation> salida = new List<Circulation>();
            //Obtiene el próximo tren en partir a partir de esta hora...
            foreach (Circulation auxCircula in CirculationsByDay)
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
            foreach (Circulation auxCircula in CirculationsByDay)
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
		internal TimeLapseCollection TotalTimeLapse
		{
			get
			{
				TimeLapseCollection salida = new TimeLapseCollection();
				foreach (Circulation cir in  CirculationsByDay)
                    salida.Add(cir.TimeLapse);
				return salida;
			}
		}

		internal Circulation? currentCirculation { get; set; }
		internal void setCurrentCirculation(string rhs)
		{
			currentCirculation = getCirculationById(rhs);
		}
		internal Circulation? getCirculationById(string rhs)
		{
			foreach (Circulation circulation in CirculationsByDay)
			{
				if (circulation.name == rhs) 
					return circulation;
            }
			return null;
		}
		internal Schedule? scheduleByCirculation(Circulation rhs)
		{
			foreach (Schedule auxSchedule in mcolSchedules)
			{
				if (auxSchedule.containsCirculation(rhs)) return auxSchedule;
			}
			return null;
		}
		internal Schedule? currentSchedule { get; set; }

		internal Plan(TopoStorage parent)
		{
			this.Parent = parent;
			Id = string.Empty;
			Name = string.Empty;
			Comment = string.Empty;
			mvarColor = new string[2];
			AllCirculationBlocks = new List<CirculationBlock>();			
			mcolSchedules = new List<Schedule>();
		}
	}
}
