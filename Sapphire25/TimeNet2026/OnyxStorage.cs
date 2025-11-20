using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using TimeNet2026.Timed;
using TimeNet2026.Topo;

namespace TimeNet2026
{
	internal class OnyxStorage
	{
		internal List<TopoStorage> mcolTopoStorage; //Colección de topologías de distintos sitios
		internal Dictionary<string, Plan> mcolPlans; //Colección de planes de explotación.
		internal OnyxStorage()
		{

			mcolPlans = new Dictionary<string, Plan>();
		}


		internal void deserializeTopo(XmlNode root)
		{
			//Root es el nodo "layout"


		}
		internal void deserializeRauta(XmlNode root)
		{

		}







		private void assignAxis()
		{
			//Ubica cada tramo de asimilación en su correspondiente eje
			Station lastStation;
			foreach (Asimilation auxAsimila in mcolAsimilations.Values)
			{
				lastStation = auxAsimila.origin;
				foreach (Asimilation.asimilationStep auxStep in auxAsimila.mcolSteps)
				{
					auxStep.axis = axisByStation(lastStation);
					lastStation = auxStep.destination;
				}
			}
		}
		internal List<Axis> getNearestAxis(Xamarin.Essentials.Location point, double range = 1000) //Obtiene el eje más cercano al punto dado
		{
			List<Axis> salida = new List<Axis>();
			double auxDistance;
			LatLng auxPoint = new LatLng(point.Latitude, point.Longitude);
			foreach (Axis eje in mcolAxis.Values)
			{
				if (eje.contains(point))
				{
					auxDistance = eje.distanceToPoint(new Xamarin.Essentials.Location(point.Latitude, point.Longitude));
					if (auxDistance < range)
						salida.Add(eje);
				}
			}
			return salida;
		}

		internal Axis getMostNearestAxis(Xamarin.Essentials.Location point, double range = 1000)
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
		internal List<temporalLimitation> getTemporalLimitations(string filter, bool onlyActives)
		{
			return mvarStorage.getTemporalLimitations(filter, onlyActives);
		}





	}
}
