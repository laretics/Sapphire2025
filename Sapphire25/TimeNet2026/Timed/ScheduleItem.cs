using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

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
				string? auxStart = node.Attributes["start"]?.Value;
				string? auxEnd = node.Attributes["end"]?.Value;
				string? auxActive = node.Attributes["active"]?.Value;
				this.active = true;
				if(null!=auxActive && auxActive.ToUpper().Contains('F')) this.active = false;
				this.timeLapse = new TimeLapse(auxStart, auxEnd);				
			}
		}
		private void ParseCirculation(XmlNode node, Plan parent)
		{
			if(null!=node.Attributes)
			{
				XmlAttribute? auxCirculation = node.Attributes?["id"];
				if (null != auxCirculation && parent.mcolCirculations.ContainsKey(auxCirculation.Value))
				{
					Circulation circ = parent.mcolCirculations[auxCirculation.Value];
					this.circulation = circ;
					this.active = true;
					string? auxActive = node.Attributes["active"]?.Value;
					if (null != auxActive && auxActive.ToUpper().Contains('F')) this.active = false;
				}
			}
		}

		internal ScheduleItem(Circulation circulation, bool active) : this(circulation.TimeLapse, active)
		{
			this.circulation = circulation;
		}
	}
}
