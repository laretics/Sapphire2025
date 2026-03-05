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
		public byte weekdayMask { get; set; }
		public string pattern { get; set; } = "####";
		public bool Ready => null != asimilation && null !=asimilation.duration;

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
				return string.Format("{0} ({1} circulaciones)", asimilation.name, Circulations.Count);
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
		}
		public CirculationBlock(XNode node, TopoStorage storage) :this()
		{
			deserializeBlock(node, storage);
		}
		public Circulation? GetCirculation(string id)
		{
			foreach(Circulation aux in Circulations)
				if (aux.name == id) return aux;
			return null;
		}

		internal void deserialize(XNode root, TopoStorage storage)
		{
			if(root is XElement element)
			{
				if (element.Name.LocalName == "block") deserializeBlock(element, storage);
				else if (element.Name.LocalName == "cir") deserializeUnit(element, storage);
			}
		}
		protected void deserializeBlock(XNode root, TopoStorage storage)
		{
			if (root is XElement element)
			{
				if ("block" == element.Name.LocalName)
				{
					deserializeCommon(root, storage);
					foreach (XNode child in element.Elements())
					{
						Circulation? nuevaCirculacion = deserializeUnit(child, storage);
						if (null != nuevaCirculacion)
							Circulations.Add(nuevaCirculacion);
					}
				}
			}
		}
		protected Circulation? deserializeUnit(XNode root, TopoStorage storage)
		{
			if (root is XElement element)
			{
				if("cir" == element.Name.LocalName)
				{
					deserializeCommon(root, storage);
					Circulation nuevaCirculacion = new Circulation(this);
					string auxTexto = XUtil.StringParam(element, "id");
					if (auxTexto.Length > 0) nuevaCirculacion.name = auxTexto;
					nuevaCirculacion.departure = XUtil.TimeSpanParam(root, "dep");
					nuevaCirculacion.color[0] = XUtil.StringParam(root, "col", "black");
					nuevaCirculacion.color[1] = XUtil.StringParam(root, "col2", "white");
					mcolCirculations.Add(nuevaCirculacion);
				}
			}
			return null;
		}
		/// <summary>
		/// Deserializa las partes que se pueden encontrar tanto en un bloque como en una circulación individual.
		/// </summary>
		/// <param name="root"></param>
		protected void deserializeCommon(XNode root, TopoStorage storage)
		{
			if(root is XElement element)
			{
				string auxTexto = XUtil.StringParam(element, "freq", "");
				if (auxTexto.Length > 0)
					weekdayMask = TNUtil.parseWeekDays(auxTexto);				
				auxTexto = XUtil.StringParam(element, "asm", "");
				if (auxTexto.Length > 0)
					asimilation = storage.GetAsimilation(auxTexto);
				pattern = XUtil.StringParam(element, "pattern", "");
			}

		}
	}
}
