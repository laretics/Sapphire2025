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
		internal TopoStorage TopoStorage { get; private set; }
		internal Rauta? Rauta { get; private set; }
		internal Plan? Plan { get; set; }
		internal AsimilationCollectionNode(TreeViewEnvironment parent, TopoStorage storage, Rauta? rauta = null, Plan? plan = null):base(parent)
		{
			this.TopoStorage = storage;
			this.Rauta = rauta;
			this.Plan = plan;
		}
		public override string Name => string.Format("{0} Asimilaciones", TopoStorage.ColAsimilations.Count());
	}
}
