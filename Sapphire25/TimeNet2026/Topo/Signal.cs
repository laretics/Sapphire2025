using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using TimeNet2026.Auxiliar;

namespace TimeNet2026.Topo
{
	internal class Signal:Punctual
	{
		internal string Name { get; set; }
		internal string Comment { get; set; }
		internal byte Track { get; set; }

		internal Signal():base()
		{
			Name = string.Empty;
			Comment = string.Empty;
		}
		internal Signal(XNode root):base()
		{
			this.pk = XUtil.LongParam(root, "pk");
			this.Track = XUtil.ByteParam(root, "par");
			this.Name = XUtil.StringParam(root, "id");
			this.Comment = XUtil.StringParam(root, "comment");
		}
		internal string XNode()
		{
			return string.Format("<item pk=\"{0}\" par=\"{1}\" id=\"{2}\" comment=\"{3}\"  />", 
				pk, 
				Track, 
				Name,
				Comment);
		}


	}
}
