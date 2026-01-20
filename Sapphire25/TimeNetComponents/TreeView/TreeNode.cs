using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimeNetComponents.TreeView
{
	public class TreeNode
	{
		internal TreeViewEnvironment Parent { get; private set; }
		public virtual string Name { get => string.Empty; }
		public virtual string SvgIcon { get => "<svg xmlns=\"http://www.w3.org/2000/svg\" height=\"16px\" viewBox=\"0 -960 960 960\" width=\"16px\" fill=\"#e3e3e3\"><path d=\"M160-160q-33 0-56.5-23.5T80-240v-480q0-33 23.5-56.5T160-800h240l80 80h320q33 0 56.5 23.5T880-640v400q0 33-23.5 56.5T800-160H160Zm0-80h640v-400H447l-80-80H160v480Zm0 0v-480 480Z\"/></svg>"; }
		public virtual List<TreeNode> Children => Parent.Children(this);
		public virtual string? Url { get => null; }
		internal TreeNode(TreeViewEnvironment parent)
		{
			this.Parent = parent;
		}
	}
}
