using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimeNet2026.Topo;

namespace TimeNetComponents.TreeView
{
	internal class StationNode:TreeNode
	{
		internal Station content { get; private set; }
		internal StationNode (Station content)
		{
			this.content = content;
		}
		public override string Name => string.Format("{0} ({1})", content.name,content.shortName);

		public override string SvgIcon => "<svg xmlns=\"http://www.w3.org/2000/svg\" height=\"24px\" viewBox=\"0 -960 960 960\" width=\"24px\" fill=\"#e3e3e3\"><path d=\"M480-80q-83 0-156-31.5T197-197q-54-54-85.5-127T80-480q0-83 31.5-156T197-763q54-54 127-85.5T480-880q83 0 156 31.5T763-763q54 54 85.5 127T880-480q0 83-31.5 156T763-197q-54 54-127 85.5T480-80Zm0-80q134 0 227-93t93-227q0-134-93-227t-227-93q-134 0-227 93t-93 227q0 134 93 227t227 93Zm0-320Z\"/></svg>";

	}
}
