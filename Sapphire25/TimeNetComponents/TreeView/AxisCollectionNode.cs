using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TimeNet2026.Topo;

namespace TimeNetComponents.TreeView
{
	internal class AxisCollectionNode:TreeNode
	{
		internal TopoStorage content { get; private set; }
		internal AxisCollectionNode(TopoStorage content)
		{
			this.content = content;			
		}
		public override string Name => string.Format("{0} Ejes", content.ColAxis.Count());
		public override List<TreeNode> Children
		{
			get
			{
				List<TreeNode> salida = new List<TreeNode>();
				foreach (Axis eje in content.ColAxis)
				{
					AxisNode nuevo = new AxisNode(eje);
					salida.Add(nuevo);
				}
				return salida;
			}
		}
	}
}
