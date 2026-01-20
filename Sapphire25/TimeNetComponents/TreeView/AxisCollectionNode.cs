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
		internal TopoStorage TopoStorage { get; private set; }
		internal AxisCollectionNode(TreeViewEnvironment parent, TopoStorage content):base(parent)
		{
			this.TopoStorage = content;			
		}
		public override string Name => string.Format("{0} Ejes", TopoStorage.ColAxis.Count());
	}
}
