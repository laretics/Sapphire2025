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
		internal string mvarId; //Identificador del plan
		internal string mvarName; //Nombre del plan
		internal string mvarComment; //Comentarios del plan
		public string[] mvarColor { get; set; }
		public string Color { get => mvarColor[0]; }
		public string Name { get => mvarName; }
        public string Id { get => mvarId; } //Identificador del plan
		public TopoStorage Parent { get; private set; }
		public int CirculationCount
		{
			get
			{
				int cuenta = 0;
				foreach(CirculationBlock bloque in CirculationBlocks)
					cuenta += bloque.mcolCirculations.Count;
				return cuenta;
			}
		}
		public IEnumerable<Schedule> Schedules { get => mcolSchedules; }
		public IEnumerable<Schedule> SchedulesByDay(byte dayOfWeek)
		{
			foreach (Schedule auxSchedule in mcolSchedules)
			{
				if ((auxSchedule.weekdayMask & (1 << (dayOfWeek - 1))) != 0) yield return auxSchedule;
			}
		}
		public List<CirculationBlock> CirculationBlocks { get; private set; }
		internal List<Schedule> mcolSchedules; //No puedo hacer un diccionario porque puede haber varios turnos con el mismo nombre en días diferentes.
		internal Schedule? Schedule(string name,byte dayOfWeek)
		{
			foreach (Schedule auxSchedule in mcolSchedules)
			{
				if ((auxSchedule.name == name) && ((auxSchedule.weekdayMask & (1 << (dayOfWeek - 1))) != 0)) return auxSchedule;
			}
			return null;
		}
		string[] Entity.color { get => mvarColor; set => mvarColor = value; }
		string Entity.name { get => mvarName; set => mvarName = value; }
		string Entity.comment { get => mvarComment; set => mvarComment = value; }
		internal Guid TopoId { get; set; } //Id de compatibilidad con la topología.
		internal List<Circulation> nextCirculationsByStation(Station station, TimeSpan time)
		{
			List<Circulation> salida = new List<Circulation>();
			//Obtiene el próximo tren en partir a partir de esta hora...
			foreach(CirculationBlock bloque in CirculationBlocks)
			{
				foreach(Circulation auxCircula in bloque.mcolCirculations)
				{
					TimeSpan auxTime = auxCircula.departureFrom(station);
					if ((auxTime < TimeSpan.MaxValue) && (auxTime >= time))
					{
						auxCircula.cacheDeparture = auxTime;
						salida.Add(auxCircula);
					}
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
			foreach(CirculationBlock bloque in CirculationBlocks)
			{
				foreach (Circulation auxCircula in bloque.mcolCirculations)
				{
					TimeSpan auxTime = auxCircula.departureFrom(station);
					auxNearest = Math.Abs(time.TotalMilliseconds - auxTime.TotalMilliseconds);
					if (auxNearest < nearestCandidate)
					{
						nearestCandidate = auxNearest;
						candidate = auxCircula;
					}
				}
			}
			return candidate;
		}
		internal TimeLapseCollection TotalTimeLapse
		{
			get
			{
				TimeLapseCollection salida = new TimeLapseCollection();
				foreach (CirculationBlock bloque in CirculationBlocks)
				{
					foreach (Circulation cir in bloque.mcolCirculations)
					{
						salida.Add(cir.TimeLapse);
					}
				}
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
			foreach (CirculationBlock bloque in CirculationBlocks)
			{
				Circulation? salida = bloque.GetCirculation(rhs);
				if (null != salida) return salida;
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
			mvarId = string.Empty;
			mvarName = string.Empty;
			mvarComment = string.Empty;
			mvarColor = new string[2];
			CirculationBlocks = new List<CirculationBlock>();			
			mcolSchedules = new List<Schedule>();
		}
		internal Plan(XNode root, TopoStorage topoStorage):this(topoStorage)
		{
			mvarId = XUtil.StringParam(root, "id");
			mvarName = XUtil.StringParam(root, "name");
			mvarComment = XUtil.StringParam(root, "comment");
			if(root is XElement element)
			{
				foreach (XElement hijo in element.Elements())
				{
					switch (hijo.Name.LocalName)
					{
						case "circulations": //Circulaciones definidas en el plan
							deserializeCirculations(hijo, Parent);
							break;
						case "schedules": //Horarios definidos en el plan
							deserializeSchedules(hijo);
							break;
					}
				}
			}
		}
		internal void deserializeCirculations(XNode root, TopoStorage topoStorage)
		{
			if(root is XElement element)
			{
				foreach (XElement hijo in element.Elements())
				{
					if (hijo.Name.LocalName == "block" || hijo.Name.LocalName == "cir")
					{
						CirculationBlock nuevoBloque = new CirculationBlock(hijo, topoStorage);
						CirculationBlocks.Add(nuevoBloque);
					}
				}
			}
		}
		internal void deserializeSchedules(XNode root)
		{
			if(root is XElement element)
			{
				foreach (XElement hijo in element.Elements())
				{
					if ("active"==hijo.Name.LocalName)
					{
						foreach (XElement nieto in hijo.Elements())
						{
							if (nieto.Name == "ws")
							{
								Schedule nuevoTurno = new Schedule();
								nuevoTurno.deserialize(nieto, this);
								mcolSchedules.Add(nuevoTurno);
							}
						}
					}
				}
			}
		}
	}
}
