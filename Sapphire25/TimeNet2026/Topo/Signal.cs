using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
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
		internal Signal(XmlNode root):base()
		{
			this.pk = XMLUtil.LongParam(root, "pk");
			this.Track = XMLUtil.ByteParam(root, "par");
			this.Name = XMLUtil.StringParam(root, "id");
			this.Comment = XMLUtil.StringParam(root, "comment");
		}
	}
}
