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

		}
		private void ParseCirculation(XmlNode node, Plan parent)
		{
			XmlAttribute? attrCirculation = node.Attributes?["id"];
			if (attrCirculation != null)
			{
				



			}
		}

		internal ScheduleItem(Circulation circulation, bool active) : this(circulation.TimeLapse, active)
		{
			this.circulation = circulation;
		}
	}
}
