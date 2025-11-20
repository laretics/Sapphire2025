using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace TimeNet2026.Timed
{
	internal class Schedule:Entity
	{		
		internal string name { get; set; }
		internal string comment { get; set; }
		internal string[] color { get; set; }
		string Entity.name { get => name; set => name = value; }
		string Entity.comment { get => comment; set => comment = value; }
		string[] Entity.color { get => this.color; set => this.color = value; }

		internal List<ScheduleItem> mcolItems;

		internal Schedule()
		{
			this.name = string.Empty;
			this.comment = string.Empty;
			this.color = new string[1];
			mcolItems = new List<ScheduleItem>();
		}
		internal bool containsCirculation(Circulation rhs)
		{
			foreach (ScheduleItem item in mcolItems)
			{
				if (item.circulation == rhs) return true;
			}
			return false;
		}

		internal void deserialize(XmlNode root, Plan parent)
		{
			name = root.Attributes["id"].Value;
			name = root.Attributes["name"].Value;
			Circulation auxCircula;
			foreach (XmlNode node in root.ChildNodes)
			{
				string circulationId = node.Attributes["id"].Value;
				if (parent.mcolCirculations.ContainsKey(circulationId))
				{
					ScheduleItem auxItem = new ScheduleItem();
					auxItem.active = node.Attributes["v"] == null;
					auxItem.circulation = parent.mcolCirculations[circulationId];
					mcolItems.Add(auxItem);
				}
			}
		}

		internal struct ScheduleItem
		{
			internal Circulation circulation { get; set; }
			internal bool active { get; set; } //Indica si el maquinista trabaja en esta parte
			internal ScheduleItem(Circulation circulation, bool active)
			{
				this.circulation = circulation;
				this.active = active;
			}
		}


	}
}
