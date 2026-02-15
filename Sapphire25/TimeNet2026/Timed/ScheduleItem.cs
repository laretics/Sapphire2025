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
		internal ScheduleItem (XNode node, Plan parent):this(new TimeLapse(null,null),false)
		{
			if(node is XElement element)
			{
				if (element.Name.LocalName.Equals("train"))
					ParseCirculation(element, parent);
				else if (element.Name.LocalName.Equals("depot"))
					ParseDepot(element);
			}
		}		
		private void ParseDepot(XElement node)
		{
			TimeSpan auxStart = XUtil.TimeSpanParam(node, "start");
			TimeSpan auxEnd = XUtil.TimeSpanParam(node, "end");				
			this.active = XUtil.BoolParam(node, "active",true);
			this.timeLapse = new TimeLapse(auxStart, auxEnd);				
		}

		//ToDo: Implementar esto.
		internal string XNode()
		{
			return string.Empty;
		}
		private void ParseCirculation(XElement node, Plan parent)
		{
			string auxCirculationId = XUtil.StringParam(node, "id", "");
			if (auxCirculationId.Length > 0)
			{
				Circulation? circula = parent.getCirculationById(auxCirculationId);
				if (null != circula)
				{
					this.circulation = circula;
					this.timeLapse = circula.TimeLapse;
					this.active = !(XUtil.StringParam(node, "active", "T").ToUpper().Contains('F'));
				}
			}
		}

		internal ScheduleItem(Circulation circulation, bool active) : this(circulation.TimeLapse, active)
		{
			this.circulation = circulation;
		}
	}
}
