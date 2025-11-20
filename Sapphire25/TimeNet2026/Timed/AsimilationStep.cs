using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimeNet2026.Topo;

namespace TimeNet2026.Timed
{
	internal class AsimilationStep
	{
		internal AsimilationStep(Station destination, Axis axis, TimeSpan tripTime, TimeSpan stopTime)
		{
			this.destination = destination;
			this.axis = axis;
			this.tripTime = tripTime;
			this.stopTime = stopTime;
		}
		internal Station destination { get; set; }
		internal Axis axis { get; set; } //Devuelve el eje al que pertenece este tramo
		internal TimeSpan tripTime { get; set; }
		internal TimeSpan stopTime { get; set; }
		internal float auxCacheY { get; set; } //Valor cacheado para representar una malla.
											   //IMPORTANTE: Este valor no tiene ninguna relevancia fuera de la operación de pintado.
	}
}
