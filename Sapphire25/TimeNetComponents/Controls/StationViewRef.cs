using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimeNet2026.Topo;

namespace TimeNetComponents.Controls
{
	internal class StationViewRef
	{
		public Axis Axis { get; set; } //Eje comprendido desde la referencia anterior, hasta la siguiente.
		public int Index { get; set; }
		public long ViewPk { get; set; }
		public StationViewRef(Axis axis, int index, long viewPk, bool isStation)
		{
			this.Axis = axis;
			this.Index = index;
			this.ViewPk = viewPk;
			this.IsStation = isStation;
			
		}
		public bool IsStation { get; private set; }
	}
}
