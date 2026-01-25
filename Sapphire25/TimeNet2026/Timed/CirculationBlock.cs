using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
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
		internal List<Circulation> mcolCirculations;
		public Asimilation? asimilation { get; set; }
		public byte weekdayMask { get; set; }
		public string pattern { get; set; } = "####";
		public bool Ready => null != asimilation && null !=asimilation.duration;

		public TimeLapseCollection? TimeLapse
		{
			get
			{
				
				if(mcolCirculations.Count>0)
				{
					TimeLapseCollection salida = new TimeLapseCollection();
					foreach (Circulation cir in mcolCirculations)
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
		public int Count => mcolCirculations.Count;
		public List<Circulation> Circulations => mcolCirculations;
		public override string ToString()
		{
			if(null==asimilation)
				return "?? (No asimilación)";
			else
				return string.Format("{0} ({1} circulaciones)", asimilation.name, mcolCirculations.Count);
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
			mcolCirculations = new List<Circulation>();
		}
		public CirculationBlock(XmlNode node, TopoStorage storage) :this()
		{
			deserializeBlock(node, storage);
		}
		public Circulation? GetCirculation(string id)
		{
			foreach(Circulation aux in mcolCirculations)
				if (aux.name == id) return aux;
			return null;
		}

		internal void deserialize(XmlNode root, TopoStorage storage)
		{
			if(root.Name=="block") deserializeBlock(root, storage);
			else if (root.Name=="cir") deserializeUnit(root, storage);
		}
		protected void deserializeBlock(XmlNode root, TopoStorage storage)
		{
			if("block" == root.Name)
			{
				deserializeCommon(root,storage);
				foreach(XmlNode child in root.ChildNodes)
				{
					Circulation? nuevaCirculacion = deserializeUnit(child, storage);
					if (null != nuevaCirculacion)
						mcolCirculations.Add(nuevaCirculacion);
				}
			}
		}
		protected Circulation? deserializeUnit(XmlNode root, TopoStorage storage)
		{
			if("cir"==root.Name)
			{
				deserializeCommon(root, storage);
				Circulation nuevaCirculacion = new Circulation(this);
				string auxTexto = XMLUtil.StringParam(root, "id", "");
				if (auxTexto.Length>0) nuevaCirculacion.name = auxTexto;
				nuevaCirculacion.departure= XMLUtil.TimeSpanParam(root, "dep");				
				nuevaCirculacion.color[0] = XMLUtil.StringParam(root, "col", "black");
				nuevaCirculacion.color[1] = XMLUtil.StringParam(root, "col", "white");
				mcolCirculations.Add(nuevaCirculacion);
			}
			return null;
		}
		/// <summary>
		/// Deserializa las partes que se pueden encontrar tanto en un bloque como en una circulación individual.
		/// </summary>
		/// <param name="root"></param>
		protected void deserializeCommon(XmlNode root, TopoStorage storage)
		{
			string auxTexto = XMLUtil.StringParam(root, "freq", "");
			if (auxTexto.Length > 0)
				weekdayMask = TNUtil.parseWeekDays(auxTexto);
			auxTexto = XMLUtil.StringParam(root, "pattern", "");
			if (auxTexto.Length > 0)
				pattern = auxTexto;
			auxTexto  = XMLUtil.StringParam(root, "asm", "");
			if (auxTexto.Length > 0)
				asimilation = storage.GetAsimilation(auxTexto);
		}
	}
}
