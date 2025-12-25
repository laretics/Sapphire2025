using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimeNet2026.Timed;

namespace TimeNetComponents.TreeView
{
	internal class PlansNode:TreeNode
	{
		public IEnumerable<Plan> content { get; private set; }
		public PlansNode(IEnumerable<Plan> content)
		{
			this.content = content;
		}
		public override string Name => string.Format("{0} Planes", content.Count());
		public override List<TreeNode> Children
		{
			get
			{
				List<TreeNode> salida = new List<TreeNode>();

				return salida;
			}
		}

	}
}
