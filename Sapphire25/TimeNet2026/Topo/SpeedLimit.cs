using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using TimeNet2026.Auxiliar;

namespace TimeNet2026.Topo
{
	internal class SpeedLimit:Lineal, Entity
	{
		
		public int Speed { get; set; }
		public bool Temporal { get; set; }
		private string mvarComment;
		private string[] mcolColor;
		internal SpeedLimit()
		{
			mvarComment = string.Empty;
			mcolColor = new string[1];
			Speed = 0;
		}
		internal SpeedLimit(XmlNode root):base(root)
		{
			this.Speed = XMLUtil.IntParam(root, "speed");			
			mvarComment = XMLUtil.StringParam(root, "comment");
			mcolColor = new string[1];
		}
		public string name 
		{
			get => string.Format("{0}Km/h{1}", Speed, Temporal ? "(T)" : "");
			set
			{
				if (int.TryParse(value, out int result))
					Speed = result;
			}
		}
		public string comment { get => mvarComment; set => mvarComment = value; }
		public string[] color { get => mcolColor; set => mcolColor = value; }
	}
}
