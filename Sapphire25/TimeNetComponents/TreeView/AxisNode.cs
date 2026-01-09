using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimeNet2026.Topo;

namespace TimeNetComponents.TreeView
{
	internal class AxisNode:TreeNode
	{
		internal Axis Axis { get; private set; }
		internal TopoStorage TopoStorage { get; private set; }
		internal AxisNode(Axis content, TopoStorage topoStorage)
		{
			this.TopoStorage = topoStorage;
			this.Axis = content;
		}
		public override string Name => Axis.mvarName;
		public override string SvgIcon => string.Format("<svg xmlns=\"http://www.w3.org/2000/svg\" height=\"16px\" viewBox=\"0 -960 960 960\" width=\"16px\" fill=\"{0}\"><path d=\"M600-80v-100L320-320H120v-240h172l108-124v-196h240v240H468L360-516v126l240 120v-50h240v240H600ZM480-720h80v-80h-80v80ZM200-400h80v-80h-80v80Zm480 240h80v-80h-80v80ZM520-760ZM240-440Zm480 240Z\"/></svg>", ((TimeNet2026.Entity)Axis).color);
		public override List<TreeNode> Children
		{
			get
			{
				List<TreeNode> salida = new List<TreeNode>();
				foreach (Station estacion in Axis.Stations)
				{
					StationNode nuevo = new StationNode(TopoStorage,Axis, estacion);
					salida.Add(nuevo);
				}
				return salida;
			}
		}
	}
}
