using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
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
		internal ScheduleItem (XmlNode node, Plan parent):this(new TimeLapse(null,null),false)
		{
			if (node.Name.Equals("train"))
				ParseCirculation(node, parent);
			else if (node.Name.Equals("depot"))
				ParseDepot(node);
		}		
		private void ParseDepot(XmlNode node)
		{
			if(null!=node.Attributes)
			{				
				TimeSpan auxStart = XMLUtil.TimeSpanParam(node, "start");
				TimeSpan auxEnd = XMLUtil.TimeSpanParam(node, "end");				
				string? auxActive = node.Attributes["active"]?.Value;
				this.active = !(XMLUtil.StringParam(node, "active", "T").ToUpper().Contains('F'));
				this.active = true;
				this.timeLapse = new TimeLapse(auxStart, auxEnd);				
			}
		}
		private void ParseCirculation(XmlNode node, Plan parent)
		{
			if(null!=node.Attributes)
			{
				string auxCirculationId = XMLUtil.StringParam(node, "id", "");
				if(auxCirculationId.Length>0)
				{
					Circulation? circula = parent.getCirculationById(auxCirculationId);
					if(null!=circula)
					{
						this.circulation = circula;
						this.timeLapse = circula.TimeLapse;
						this.active = !(XMLUtil.StringParam(node, "active", "T").ToUpper().Contains('F'));
					}
				}
			}
		}

		internal ScheduleItem(Circulation circulation, bool active) : this(circulation.TimeLapse, active)
		{
			this.circulation = circulation;
		}
	}
}
