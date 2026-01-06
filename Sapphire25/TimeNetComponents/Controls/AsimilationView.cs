using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimeNet2026.Timed;
using TimeNet2026.Topo;

namespace TimeNetComponents.Controls
{
	/// <summary>
	/// Vista de asimilación para diagramas de malla. Contiene las estaciones y puntos singulares del trayecto de una asimilación con la información suficiente para poder representar la malla en orden de puntos singulares o por PK.
	/// </summary>
	internal class AsimilationView
	{
		internal Asimilation Parent { get; set; }
		internal TopoStorage Storage { get; set; }
		internal long MaxPk { get; private set; }
		internal int MaxIndex { get; private set; }
		internal Dictionary<Station,StationViewRef> Elements { get; private set; }
		internal AsimilationView(Asimilation parent, TopoStorage storage)
		{
			this.Parent = parent;
			this.Storage = storage;
			Elements = new Dictionary<Station, StationViewRef>();
			calculateReferences();
		}

		private void calculateReferences()
		{
			Elements.Clear();
			Debug.Assert(null != Parent.origin);
			if(Parent.Steps.Count()>0)
			{
				Station lastStation = Parent.origin;
				int auxIndex = 0;
				long cumulPk = 0;
				Axis currentAxis = Parent.Steps.First().axis;
				StationViewRef nueva = new StationViewRef(currentAxis, auxIndex++, cumulPk);
				Elements.Add(lastStation, nueva);
				foreach (AsimilationStep paso in Parent.Steps)
				{
					if(paso.axis==currentAxis)
						cumulPk += Math.Abs(paso.destination.pk - lastStation.pk);

					nueva = new StationViewRef(paso.axis, auxIndex++, cumulPk);

					Elements.Add(paso.destination, nueva);
					currentAxis = paso.axis;
					lastStation = paso.destination;
				}
				MaxPk = cumulPk;
				MaxIndex = auxIndex-1;
			}
		}


	}
}
