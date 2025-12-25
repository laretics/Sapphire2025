using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimeNet2026.Timed;

namespace TimeNetComponents.TreeView
{
	internal class PlanNode:TreeNode
	{
		public Plan content { get; private set; }
		public PlanNode(Plan content)
		{
			this.content = content;
		}
        public override string SvgIcon => "<svg xmlns=\"http://www.w3.org/2000/svg\" enable-background=\"new 0 0 24 24\" height=\"16px\" viewBox=\"0 0 24 24\" width=\"16px\" fill=\"grey\"><g><rect fill=\"none\" height=\"24\" width=\"24\"/><path d=\"M20,6h-8l-2-2H4C2.9,4,2.01,4.9,2.01,6L2,18c0,1.1,0.9,2,2,2h16c1.1,0,2-0.9,2-2V8C22,6.9,21.1,6,20,6z M20,18L4,18V6h5.17 l2,2H20V18z M17.5,12.12v3.38l-3,0v-5h1.38L17.5,12.12z M13,9v8l6,0v-5.5L16.5,9H13z\"/></g></svg>";
		public override string Name => string.Format("{0} ({1})", content.Name, content.Id);
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
