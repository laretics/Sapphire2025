using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using TimeNet2026.Auxiliar;
using TimeNet2026.DBStorage;
using TimeNet2026.Storage;
using TimeNet2026.Topo;

namespace TimeNet2026.Timed
{
	public class Circulation : Entity, IComparable<Circulation>
	{
		private string mvarName;
		private string mvarComment;
		private string[] mvarColor;		
		public Circulation(CirculationBlock parent)
		{
			mvarColor = new string[2];
			mvarName = "??";
			mvarComment = string.Empty;
			this.Parent = parent;
		}
		public CirculationBlock Parent { get; private set; }
		public TimeLapse TimeLapse => new TimeLapse { Begin = departure, End = arrival };
		public override bool Equals(object? obj)
		{
			if (obj is not Circulation other)
				return false;
			// Si name es único, basta con comparar name (ignora mayúsculas/minúsculas si procede)
			return string.Equals(this.name, other.name, StringComparison.Ordinal);
		}

		public override int GetHashCode()
		{
			// Usa name para el hash, asegurando que no sea null
			return name?.GetHashCode(StringComparison.Ordinal) ?? 0;
		}

		internal bool ParentReady => null != Parent && Parent.Ready;
		public TimeSpan departure { get; set; }
		public TimeSpan arrival
		{
			get
			{
				if (ParentReady)
					return departure.Add(Parent.Duration);				
				else
					return departure;
			}
		}
		internal TimeSpan calculateDelay(long currentPk, TimeSpan currentTime)
		{
			if(!ParentReady) return new TimeSpan(0);			
			return Parent.CalculateDelay(currentPk, currentTime.Subtract(departure));
		}
		public int CompareTo(Circulation other) { return cacheDeparture.CompareTo(other.cacheDeparture); }
		internal bool contained(TimeSpan begin, TimeSpan end) { return !((departure > end) || (arrival < begin)); }
		internal TimeSpan departureFrom(Station station)
		{
			if (!ParentReady) return TimeSpan.MaxValue;
			return Parent.departureFrom(station).Add(departure);
		}
		internal TimeSpan cacheDeparture { get; set; } //Valor usado para ordenar los trenes por hora de salida.
		public string name { get => mvarName; set => mvarName = value; }
		public override string ToString() {return this.name;}
		public string TourmalineString
		{
			get => string.Format("{0} {1:hh\\:mm}", Parent.asimilation?.TourmalineString, departure);
		}
		public string comment { get => mvarComment; set => mvarComment = value; }
		public string[] color { get => mvarColor; set => mvarColor=value; }
	}
}
