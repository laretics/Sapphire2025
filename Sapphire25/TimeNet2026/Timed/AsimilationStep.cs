using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimeNet2026.Topo;

namespace TimeNet2026.Timed
{
	public class AsimilationStep
	{
		public AsimilationStep(Station destination, Axis axis, TimeSpan tripTime, TimeSpan stopTime)
		{
			this.destination = destination;
			this.axis = axis;
			this.tripTime = tripTime;
			this.stopTime = stopTime;
		}
		public AsimilationStep(AsimilationStep origin)
		{
			this.destination = origin.destination;
			this.axis = origin.axis;
			this.tripTime = origin.tripTime;
			this.stopTime = origin.stopTime;
		}
		public Station destination { get; set; }
		public Axis axis { get; set; } //Devuelve el eje al que pertenece este tramo
		public TimeSpan tripTime { get; set; }
		public TimeSpan stopTime { get; set; }
	}
}
