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
		internal AsimilationCollectionNode(TopoStorage storage, Rauta? rauta = null, Plan? plan = null)
		{
			this.TopoStorage = storage;
			this.Rauta = rauta;
			this.Plan = plan;
		}
		public override string Name => string.Format("{0} Asimilaciones", TopoStorage.ColAsimilations.Count());
		public override List<TreeNode> Children
		{
			get
			{
				List<TreeNode> salida = new List<TreeNode>();
				foreach (Asimilation asimila in TopoStorage.ColAsimilations.Values)
				{
					AsimilationNode nuevo = new AsimilationNode(asimila,TopoStorage,Rauta,Plan);
					salida.Add(nuevo);
				}
				return salida;
			}
		}
	}
}
