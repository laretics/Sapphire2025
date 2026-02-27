using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimeNet2026.Topo
{
	public class BasicAxis:Lineal, Entity
	{
		protected string mvarName { get; set; }
		protected string mvarComment { get; set; }
		internal string[] mvarColor { get; set; }
		string Entity.name { get => mvarName; set => mvarName = value; }
		string Entity.comment { get => mvarComment; set => mvarComment = value; }
		String[] Entity.color { get => mvarColor; set => mvarColor = value; }

		public BasicAxis()
		{

		}
	}
}
