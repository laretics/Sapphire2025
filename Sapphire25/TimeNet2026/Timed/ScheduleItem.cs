using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using TimeNet2026.Auxiliar;

namespace TimeNet2026.Timed
{
	internal class ScheduleItem
	{
		internal Circulation? circulation { get; set; }
		internal TimeLapse timeLapse { get; set; }
		internal bool active { get; set; } //Indica si el maquinista trabaja en esta parte
		internal ScheduleItem(TimeLapse timeLapse, bool active)
		{
			this.timeLapse = timeLapse;
			this.active = active;
		}
		internal ScheduleItem(Circulation circulation, bool active) : this(circulation.TimeLapse, active)
		{
			this.circulation = circulation;
		}
	}
}
