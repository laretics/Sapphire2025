using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using TimeNet2026.Auxiliar;
using TimeNet2026.ScriptCompiling;
using TimeNet2026.Timed;
namespace TimeNet2026.Topo
{
	public class TopoStorage: Entity
	{
		public Header Header { get; set; } //Encabezado.
		internal Dictionary<string, Axis> mcolAxis; //Colección de ejes	
		internal Dictionary<string, Asimilation> mcolAsimilations; //Colección de asimilaciones.
		private Dictionary<Guid,Rauta> mcolRauta; //Colección de paquetes de importación.
		string Entity.name { get => Header.Name; set => Header.Name=value; }
		string Entity.comment { get => Header.Comment; set => Header.Comment=value; }
		string[] Entity.color { get => mcolColor; set => mcolColor=value; }
		private string[] mcolColor = new string[2] { "#000000", "#FFFFFF" };
		public TopoStorage()
		{
			Header = new Header();
			mcolAsimilations = new Dictionary<string, Asimilation>();
			mcolAxis = new Dictionary<string, Axis>();
			mcolRauta = new Dictionary<Guid, Rauta>();			
		}
		public IEnumerable<Axis> ColAxis { get => mcolAxis.Values; }
		public Dictionary<string,Asimilation> ColAsimilations { get => mcolAsimilations; }
		public Dictionary<Guid,Rauta> ColRauta { get => mcolRauta; }
		public Asimilation? GetAsimilation(string? id)
		{
			if (null == id) return null;
			if (mcolAsimilations.ContainsKey(id))
				return mcolAsimilations[id];
			return null;
		}
		internal List<Axis> getNearestAxis(GeoLocation point, double range = 1000) //Obtiene el eje más cercano al punto dado
		{
			List<Axis> colSalida = new List<Axis>();
			double auxDistance;
			foreach (Axis eje in mcolAxis.Values)
			{
				if(null!=eje.Topology)
				{
					if (eje.Topology.contains(point))
					{
						auxDistance = eje.Topology.distanceToPoint(point);
						if (auxDistance < range)
							colSalida.Add(eje);
					}
				}
			}
			return colSalida;
		}
		internal Axis? getMostNearestAxis(GeoLocation point, double range = 1000)
		{
			List<Axis> col = getNearestAxis(point, range);
			if (0 == col.Count) return null;
			if (col.Count == 1) return col[0];
			Axis candidate = col[0];
			if(null!=candidate.Topology)
			{
				double auxDistance = candidate.Topology.distanceToPoint(point);
				for (int i = 1; i < col.Count; i++)
				{
					if (null != col[i].Topology)
					{
						double auxThisDistance = col[i].Topology.distanceToPoint(point);
						if (auxThisDistance < auxDistance)
						{
							auxDistance = auxThisDistance;
							candidate = col[i];
						}
					}
				}
			}
			return candidate;
		}
		public Axis? axisByStation(Station? rhs)
		{
			if (null == rhs) return null;
			foreach (Axis eje in mcolAxis.Values)
				if (eje.contains(rhs)) return eje;
			return null;
		}
		public Station? stationById(string id)
		{
			Station? salida = null;
			foreach(Axis eje in mcolAxis.Values)
			{
				salida = eje.stationById(id);
				if (null != salida) return salida;
			}
			return null;
		}
	
		internal bool InstallRauta(Rauta rauta)
		{
			if (mcolRauta.ContainsKey(rauta.Header.Id))
				mcolRauta[rauta.Header.Id] = rauta;
			else
				mcolRauta.Add(rauta.Header.Id, rauta);
			return true;
		}
		internal IEnumerable<Rauta> Rautatie { get => mcolRauta.Values.ToList(); }
	}
}
