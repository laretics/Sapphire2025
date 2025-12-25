using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimeNet2026.Timed;
using TimeNet2026.Topo;

namespace TimeNetComponents.TreeView
{
	internal class AsimilationCollectionNode:TreeNode
	{
		internal TopoStorage content { get; private set; }
		internal AsimilationCollectionNode(TopoStorage content)
		{
			this.content = content;
		}
		public override string Name => string.Format("{0} Asimilaciones", content.ColAsimilations.Count());
		public override List<TreeNode> Children
		{
			get
			{
				List<TreeNode> salida = new List<TreeNode>();
				foreach (Asimilation asimila in content.ColAsimilations)
				{
					AsimilationNode nuevo = new AsimilationNode(asimila);
					salida.Add(nuevo);
				}
				return salida;
			}
		}
	}
}
