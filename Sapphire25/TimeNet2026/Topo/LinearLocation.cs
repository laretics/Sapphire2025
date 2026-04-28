using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimeNet2026.Timed;

namespace TimeNet2026.Topo
{
	/// <summary>
	/// Ónice
	/// Esta es la parte más importante de Ónice
	/// Almacena una ubicación lineal.
	/// La ubicación lineal se puede asignar a mano, se obtiene por GPS o se actualiza por cuentakilómetros.
	/// </summary>
	public class LinearLocation
	{
		public Axis? Axis { get; internal set; } = null; //Eje en el que estamos.
		public Asimilation? Asimilation { get; set; } = null; //Si estamos siguiendo a un tren, sólo trabajamos con sus ejes
		public long PKRef { get; internal set; } = -1; //Distancia en metros al origen del eje.
		public LinearLocationSource Source { get; internal set; } //Procedencia del último dato actualizado.

		public DateTime LastManualInput { get; internal set; } = DateTime.MinValue; //Antigüedad del último dato introducido a mano.
		public DateTime LastOdometerUpdate{ get;internal set; } = DateTime.MinValue; //Antigüedad del último dato actualizado por odómetro.
		public DateTime LastSatelliteInput{ get; internal set; } = DateTime.MinValue; //Antigüedad de la última lectura por satélite.

		public bool TryLocateBySatellite(GeoLocation geo, TopoStorage? storage, double range = 1000)
		{
			//Lo primero que vamos a hacer es buscar un eje cercano.
			Axis? auxAxis = null;
			if (null == storage) return false;
			if (null == Asimilation)
				auxAxis = storage.getMostNearestAxis(geo, range);
			else
				auxAxis = Asimilation.axisByGeoLocation(geo);
				

			if(null!=auxAxis && null!=auxAxis.Topology)
			{
				long auxSalida = auxAxis.Topology.getPk(geo);
				if(auxSalida>=0)
				{
					PKRef = auxSalida;
					Axis = auxAxis;
					Source = LinearLocationSource.Satellite;
					LastSatelliteInput = DateTime.Now;
				}
			}
			return false;
		}

	}
}
