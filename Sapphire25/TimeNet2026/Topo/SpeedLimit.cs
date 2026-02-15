using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using TimeNet2026.Auxiliar;
using TimeNet2026.Storage;

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
		internal SpeedLimit(XNode root):base(root)
		{
			this.Speed = XUtil.IntParam(root, "speed");
			mvarComment = XUtil.StringParam(root, "comment");
			mcolColor = new string[1];
		}
		internal override string XNode()
		{
			return string.Format("<item pk0=\"{0}\" pkf=\"{1}\" par=\"{2}\" speed=\"{3}\" comment=\"{4}\" />",
				pk, 
				pkEnd, 
				Tracks,
				Speed,
				comment);
		}
		internal static new List<OnyxField> Descriptor()
		{
			List<OnyxField> salida = Lineal.Descriptor();
			salida.Add(new OnyxField("id", "STRING", true, true, false));
			salida.Add(new OnyxField("name", "STRING"));
			salida.Add(new OnyxField("comment", "STRING"));
			salida.Add(new OnyxField("color0", "STRING"));
			salida.Add(new OnyxField("color1", "STRING"));
			return salida;
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
