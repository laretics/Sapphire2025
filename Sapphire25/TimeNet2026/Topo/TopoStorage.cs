using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using TimeNet2026.Auxiliar;
using TimeNet2026.Timed;
namespace TimeNet2026.Topo
{
	public class TopoStorage
	{
		public Header Header { get; set; } //Encabezado.
		internal Dictionary<string, Axis> mcolAxis; //Colección de ejes	
		internal Dictionary<string, Asimilation> mcolAsimilations; //Colección de asimilaciones.
		public TopoStorage()
		{
			Header = new Header();
			mcolAsimilations = new Dictionary<string, Asimilation>();
			mcolAxis = new Dictionary<string, Axis>();
		}
		public List<Axis> ColAxis { get => mcolAxis.Values.ToList(); }
		public List<Asimilation> ColAsimilations { get => mcolAsimilations.Values.ToList(); }
		public TopoStorage(XmlNode root):this()
		{
			foreach (XmlNode hijo in root.ChildNodes)
			{
				switch (hijo.Name)
				{
					case "info": //Cabecera de información.
						this.Header.deserialize(hijo);
						break;
					case "topo": //Ejes
						importAxis(hijo);
						break;
					case "asimilation": //Asimilaciones
						deserializeAsimilations(hijo);
						break;
				}
			}
		}
		internal void importAxis(XmlNode root)
		{
			foreach (XmlNode hijo in root.ChildNodes)
			{
				if (hijo.Name.Equals("axis"))
				{
					Axis nuevo = new Axis(hijo);
					if (mcolAxis.ContainsKey(nuevo.id))
						mcolAxis.Remove(nuevo.id);
					mcolAxis.Add(nuevo.id, nuevo);
				}
			}
		}
		internal List<Axis> getNearestAxis(GeoLocation point, double range = 1000) //Obtiene el eje más cercano al punto dado
		{
			List<Axis> colSalida = new List<Axis>();			
			double auxDistance;
			foreach (Axis eje in mcolAxis.Values)
			{
				if (eje.contains(point))
				{
					auxDistance = eje.distanceToPoint(point);
					if (auxDistance < range)
						colSalida.Add(eje);
				}
			}
			return colSalida;
		}
		internal Axis getMostNearestAxis(GeoLocation point, double range = 1000)
		{
			List<Axis> col = getNearestAxis(point, range);
			if (0 == col.Count) return null;
			if (col.Count == 1) return col[0];
			Axis candidate = col[0];
			double auxDistance = candidate.distanceToPoint(point);
			for (int i = 1; i < col.Count; i++)
			{
				double auxThisDistance = col[i].distanceToPoint(point);
				if (auxThisDistance < auxDistance)
				{
					auxDistance = auxThisDistance;
					candidate = col[i];
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
		internal void deserializeAsimilations(XmlNode root)
		{
			foreach(XmlNode hijo in root.ChildNodes)
			{
				if("item"==hijo.Name)
				{
					Asimilation nueva = deserializeAsimilation(hijo);
					mcolAsimilations.Add(nueva.id, nueva);
				}					
			}
		}
		internal Asimilation deserializeAsimilation(XmlNode root)
		{
			Station? currentStation = null;
			Axis? auxCurrentAxis = null;			
			currentStation = stationById(XMLUtil.StringParam(root, "origin"));
			auxCurrentAxis = axisByStation(currentStation);
			Asimilation currentAsimilation = new Asimilation(root);
			currentAsimilation.origin = currentStation;			
			foreach(XmlNode hijo in root.ChildNodes)
			{
				if("trip"==hijo.Name)
				{
					currentStation = stationById(XMLUtil.StringParam(hijo, "dest"));
					auxCurrentAxis = axisByStation(currentStation);
					if(null!=currentStation && null!=auxCurrentAxis)
					{
						AsimilationStep paso = new AsimilationStep(currentStation,
							auxCurrentAxis,
							XMLUtil.TimeSpanParam(hijo, "time"),
							XMLUtil.TimeSpanParam(hijo, "stop"));
						currentAsimilation.mcolSteps.Add(paso);
					}
				}
			}
			return currentAsimilation;
		}
	}
}
