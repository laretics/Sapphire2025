using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using TimeNet2026.Auxiliar;
using TimeNet2026.Topo;

namespace TimeNet2026.Timed
{
	/// <summary>
	/// Nuevo contenedor de circulaciones.
	/// Se basa en el hecho de que es posible agrupar circulaciones por asimilación y días de funcionamiento.
	/// Esta estructura también preparará la generación automática de mallas.
	/// </summary>
	public class CirculationBlock
	{
		public List<Circulation> Circulations { get; internal set; }
		public Asimilation? asimilation { get; set; }
		public Weekday weekdayMask { get; set; }
		public string pattern { get; set; } = "####";
		public bool Ready => null != asimilation && null !=asimilation.duration;
		private string[] mvarColor;
		public string[] color { get => mvarColor; set => mvarColor = value; }

		public TimeLapseCollection? TimeLapse
		{
			get
			{				
				if(Circulations.Count>0)
				{
					TimeLapseCollection salida = new TimeLapseCollection();
					foreach (Circulation cir in Circulations)
						salida.Add(cir.TimeLapse);					
				}
				return null;
			}
		}
		internal TimeSpan Duration
		{
			get
			{
				if (null == asimilation || null == asimilation.duration)
					return TimeSpan.Zero;
				else
					return (TimeSpan)asimilation.duration;
			}
		}
		public int Count => Circulations.Count;
		public override string ToString()
		{
			if(null==asimilation)
				return "?? (No asimilación)";
			else
				return string.Format("{0} ({1} circulaciones)", asimilation.Name, Circulations.Count);
		}
		internal TimeSpan CalculateDelay(long pk, TimeSpan currentTime)
		{
			if (null == asimilation)
				return new TimeSpan(0);
			return asimilation.calculateDelay(pk, currentTime);
		}
		internal TimeSpan departureFrom(Station station)
		{
			if (null == asimilation) return TimeSpan.Zero;
			return asimilation.departureFrom(station);
		}
		public CirculationBlock()
		{
			Circulations = new List<Circulation>();
			mvarColor = new string[2];
		}
		public Circulation? GetCirculation(string id)
		{
			foreach(Circulation aux in Circulations)
				if (aux.name == id) return aux;
			return null;
		}
	}
}
