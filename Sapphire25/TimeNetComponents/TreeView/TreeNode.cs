using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimeNet2026.Topo;
using TimeNet2026.Timed;
using TimeNet2026.Production;
using System.Reflection.Metadata.Ecma335;

namespace TimeNetComponents.TreeView
{
	public class TreeNode
	{
		public NodeType Type { get; protected set; }
		internal TreeViewEnvironment Parent { get; private set; }
		internal TimeNetEnvironment NetEnvironment { get; private set; }
		internal TreeNode? ParentNode { get; private set;  }
		internal string ContentId { get; set; } = string.Empty; //ID de la referencia que contiene este nodo.
		internal string? Url { get; set; } //URL a la que debe saltar cuando se pulse el nodo.
		public TimeNet2026.Entity? EntityLinked
		{
			get
			{
				switch(Type)
				{
					case NodeType.Axis: return NetEnvironment.Axis;
					case NodeType.Asimilation: return NetEnvironment.Asimilation;
					case NodeType.Station:
						if (null != NetEnvironment.TopoStorage)
							return NetEnvironment.TopoStorage.stationById(ContentId);
						else
							return null;
					case NodeType.Circulation: return NetEnvironment.Circulation;
					default:
						return null;
				}
			}
		}
		public string Name 
		{ 
			get
			{
				switch (Type)
				{
					case NodeType.OnyxStorage:
						return "TimeNet2026";
					case NodeType.TopoStorage:
						return NetEnvironment.TopoStorage != null ? NetEnvironment.TopoStorage.Header.Name : "[Unknown Storage]";
					case NodeType.AxisCollection:
						return NetEnvironment.TopoStorage != null ? string.Format("{0} Ejes", NetEnvironment.TopoStorage.ColAxis.Count()) : "Ejes";
					case NodeType.Axis:
						if(null!=NetEnvironment.Axis)
							return NetEnvironment.Axis.name;
						else
							return "[Unknown Axis]";
					case NodeType.AsimilationCollection:
								return NetEnvironment.TopoStorage != null ? string.Format("{0} asimilaciones", NetEnvironment.TopoStorage.ColAsimilations.Count()) : "Asimilaciones";
					case NodeType.Asimilation:
						if(null == NetEnvironment.Asimilation || null==NetEnvironment.ViewAsimilation)
							return "[Unknown Asimilation]";
						else
						{
							if (null == NetEnvironment.Plan)
								return string.Format("{0}/{1} ({2})", NetEnvironment.ViewAsimilation.id, NetEnvironment.Asimilation.name, NetEnvironment.Asimilation.comment);
							else
								return string.Format("{0}/{1} ({2})", NetEnvironment.ViewAsimilation.id, NetEnvironment.Asimilation.name, NetEnvironment.Plan.Circulations.Count());
						}
					case NodeType.Station:
						if (null != NetEnvironment.TopoStorage)
						{
							Station? auxStation = NetEnvironment.TopoStorage.stationById(ContentId);
							if (null != auxStation)
								return string.Format("{0} ({1})", auxStation.name, auxStation.shortName);
						}
						return "[Unknown Station]";
					case NodeType.Rautatie:
						if (null != NetEnvironment.Rauta)
							return string.Format("{0}({1},{2}) {3} planes",
							NetEnvironment.Rauta.Header.Name,
							NetEnvironment.Rauta.Header.Version,
							NetEnvironment.Rauta.Header.Author,
							NetEnvironment.Rauta.Plans.Count());
						return "[Unknown Rautatie]";
					case NodeType.Plan:
						if (null != NetEnvironment.Plan)
							return string.Format("{0} {2} ({1} circulaciones)", NetEnvironment.Plan.Name, NetEnvironment.Plan.Circulations.Count(), NetEnvironment.Plan.Id);
						return "[Unknown Plan]";
					case NodeType.Circulations:
						if (null != NetEnvironment.Plan)
							return string.Format("{0} circulaciones", NetEnvironment.Plan.Circulations.Count());
						return "[Unknown Circulations]";
					case NodeType.Circulation:
						if (null != NetEnvironment.Circulation)
							return string.Format("{0} ({1}-{2})",
								NetEnvironment.Circulation.name,
								NetEnvironment.Circulation.departure.ToString(@"hh\:mm"),
								NetEnvironment.Circulation.arrival.ToString(@"hh\:mm"));
						return "[Unknown Circulation]";
					}
				return "[Unknown Element]";
			}
		}
		public virtual string SvgIcon 
		{ 
			get
			{
				string auxColor = "grey";
				switch(Type)
				{
					case NodeType.OnyxStorage:
						return "<svg xmlns=\"http://www.w3.org/2000/svg\" height=\"16px\" viewBox=\"0 -960 960 960\" width=\"16px\" fill=\"grey\"><path d=\"M480-120q-151 0-255.5-46.5T120-280v-400q0-66 105.5-113T480-840q149 0 254.5 47T840-680v400q0 67-104.5 113.5T480-120Zm0-479q89 0 179-25.5T760-679q-11-29-100.5-55T480-760q-91 0-178.5 25.5T200-679q14 30 101.5 55T480-599Zm0 199q42 0 81-4t74.5-11.5q35.5-7.5 67-18.5t57.5-25v-120q-26 14-57.5 25t-67 18.5Q600-528 561-524t-81 4q-42 0-82-4t-75.5-11.5Q287-543 256-554t-56-25v120q25 14 56 25t66.5 18.5Q358-408 398-404t82 4Zm0 200q46 0 93.5-7t87.5-18.5q40-11.5 67-26t32-29.5v-98q-26 14-57.5 25t-67 18.5Q600-328 561-324t-81 4q-42 0-82-4t-75.5-11.5Q287-343 256-354t-56-25v99q5 15 31.5 29t66.5 25.5q40 11.5 88 18.5t94 7Z\"/></svg>";
					case NodeType.TopoStorage:
						return "<svg xmlns=\"http://www.w3.org/2000/svg\" height=\"16px\" viewBox=\"0 -960 960 960\" width=\"16px\" fill=\"gray\"><path d = \"m600-120-240-84-186 72q-20 8-37-4.5T120-170v-560q0-13 7.5-23t20.5-15l212-72 240 84 186-72q20-8 37 4.5t17 33.5v560q0 13-7.5 23T812-192l-212 72Zm-40-98v-468l-160-56v468l160 56Zm80 0 120-40v-474l-120 46v468Zm-440-10 120-46v-468l-120 40v474Zm440-458v468-468Zm-320-56v468-468Z\"/></svg>";
					case NodeType.Axis:
						if(null!=NetEnvironment.Axis)
						{
							Axis auxEje = NetEnvironment.Axis;
							auxColor = ((TimeNet2026.Entity)auxEje).color[0];
						}							
						return string.Format("<svg xmlns=\"http://www.w3.org/2000/svg\" height=\"16px\" viewBox=\"0 -960 960 960\" width=\"16px\" fill=\"{0}\"><path d=\"M600-80v-100L320-320H120v-240h172l108-124v-196h240v240H468L360-516v126l240 120v-50h240v240H600ZM480-720h80v-80h-80v80ZM200-400h80v-80h-80v80Zm480 240h80v-80h-80v80ZM520-760ZM240-440Zm480 240Z\"/></svg>", auxColor);
					case NodeType.Asimilation:						
						if(null!=NetEnvironment.Asimilation)
							auxColor = NetEnvironment.Asimilation.color[0];
						return string.Format("<svg xmlns=\"http://www.w3.org/2000/svg\" height=\"16px\" viewBox=\"0 -960 960 960\" width=\"16px\" fill=\"{0}\"><path d=\"M418-340q24 24 62 23.5t56-27.5l224-336-336 224q-27 18-28.5 55t22.5 61Zm62-460q59 0 113.5 16.5T696-734l-76 48q-33-17-68.5-25.5T480-720q-133 0-226.5 93.5T160-400q0 42 11.5 83t32.5 77h552q23-38 33.5-79t10.5-85q0-36-8.5-70T766-540l48-76q30 47 47.5 100T880-406q1 57-13 109t-41 99q-11 18-30 28t-40 10H204q-21 0-40-10t-30-28q-26-45-40-95.5T80-400q0-83 31.5-155.5t86-127Q252-737 325-768.5T480-800Zm7 313Z\"/></svg>", auxColor);
					case NodeType.Station:
						return "<svg xmlns=\"http://www.w3.org/2000/svg\" height=\"16px\" viewBox=\"0 -960 960 960\" width=\"16px\" fill=\"#e3e3e3\"><path d=\"M480-80q-83 0-156-31.5T197-197q-54-54-85.5-127T80-480q0-83 31.5-156T197-763q54-54 127-85.5T480-880q83 0 156 31.5T763-763q54 54 85.5 127T880-480q0 83-31.5 156T763-197q-54 54-127 85.5T480-80Zm0-80q134 0 227-93t93-227q0-134-93-227t-227-93q-134 0-227 93t-93 227q0 134 93 227t227 93Zm0-320Z\"/></svg>";
					case NodeType.Rautatie:
						return "<svg xmlns=\"http://www.w3.org/2000/svg\" height=\"16px\" viewBox=\"0 0 24 24\" width=\"16px\" fill=\"gray\"><path d=\"M0 0h24v24H0V0z\" fill=\"none\"/><path d=\"M2 20h20v-4H2v4zm2-3h2v2H4v-2zM2 4v4h20V4H2zm4 3H4V5h2v2zm-4 7h20v-4H2v4zm2-3h2v2H4v-2z\"/></svg>";
					case NodeType.Plan:
						return "<svg xmlns=\"http://www.w3.org/2000/svg\" enable-background=\"new 0 0 24 24\" height=\"16px\" viewBox=\"0 0 24 24\" width=\"16px\" fill=\"grey\"><g><rect fill=\"none\" height=\"24\" width=\"24\"/><path d=\"M20,6h-8l-2-2H4C2.9,4,2.01,4.9,2.01,6L2,18c0,1.1,0.9,2,2,2h16c1.1,0,2-0.9,2-2V8C22,6.9,21.1,6,20,6z M20,18L4,18V6h5.17 l2,2H20V18z M17.5,12.12v3.38l-3,0v-5h1.38L17.5,12.12z M13,9v8l6,0v-5.5L16.5,9H13z\"/></g></svg>";
					case NodeType.Circulations:
						return "<svg xmlns=\"http://www.w3.org/2000/svg\" enable-background=\"new 0 0 20 20\" height=\"16px\" viewBox=\"0 0 20 20\" width=\"16px\" fill=\"grey\"><g><rect fill=\"none\" height=\"20\" width=\"20\" x=\"0\"/></g><g><path d=\"M16.5,6H10L8,4H3.5C2.67,4,2,4.67,2,5.5v9C2,15.33,2.67,16,3.5,16h13c0.83,0,1.5-0.67,1.5-1.5v-7C18,6.67,17.33,6,16.5,6z M3.5,14.5v-9h3.88l2,2h4.12V9H12v1.5h1.5V12H12v1.5h1.5V12H15v-1.5h-1.5V9H15V7.5h1.5v7H3.5z\"/></g></svg>";
					case NodeType.Circulation:
						if(null!=NetEnvironment.Asimilation)
							auxColor = NetEnvironment.Asimilation.color[0];
						return string.Format("<svg xmlns=\"http://www.w3.org/2000/svg\" height=\"16px\" viewBox=\"0 0 24 24\" width=\"16px\" fill=\"{0}\"><path d=\"M0 0h24v24H0V0z\" fill=\"none\"/><circle cx=\"8.5\" cy=\"14.5\" r=\"1.5\"/><circle cx=\"15.5\" cy=\"14.5\" r=\"1.5\"/><path d=\"M12 2c-4 0-8 .5-8 4v9.5C4 17.43 5.57 19 7.5 19L6 20.5v.5h2l2-2h4l2 2h2v-.5L16.5 19c1.93 0 3.5-1.57 3.5-3.5V6c0-3.5-4-4-8-4zm0 2c3.51 0 4.96.48 5.57 1H6.43c.61-.52 2.06-1 5.57-1zM6 7h5v3H6V7zm12 8.5c0 .83-.67 1.5-1.5 1.5h-9c-.83 0-1.5-.67-1.5-1.5V12h12v3.5zm0-5.5h-5V7h5v3z\"/></svg>", auxColor);
				}
				return "<svg xmlns=\"http://www.w3.org/2000/svg\" height=\"16px\" viewBox=\"0 -960 960 960\" width=\"16px\" fill=\"#e3e3e3\"><path d=\"M160-160q-33 0-56.5-23.5T80-240v-480q0-33 23.5-56.5T160-800h240l80 80h320q33 0 56.5 23.5T880-640v400q0 33-23.5 56.5T800-160H160Zm0-80h640v-400H447l-80-80H160v480Zm0 0v-480 480Z\"/></svg>";
			}
		}
		
		public virtual List<TreeNode> Children => Parent.Children(this);
		public TreeNode(TreeViewEnvironment parent, TreeNode? ParentNode,  NodeType type,string NodeReference="")
		{
			this.Parent = parent;
			this.ParentNode = ParentNode;
			this.Type = type;
			this.ContentId = NodeReference;
			if (null != ParentNode)
				this.NetEnvironment = new TimeNetEnvironment(ParentNode.NetEnvironment);
			else
				this.NetEnvironment = parent.Root.NetEnvironment;			
		}
		public TreeNode(TreeViewEnvironment parent, NodeType type, TimeNetEnvironment netEnvironment)
		{
			this.Parent = parent;
			this.ParentNode = null;
			this.Type = type;
			this.NetEnvironment = netEnvironment;
		}

		public enum NodeType
		{
			OnyxStorage,
			TopoStorage,
			AxisCollection,
			AsimilationCollection,
			Asimilation,
			Station,
			Rautatie,
			Axis,
			Plan,
			Circulations,
			Circulation,
		}


	}
}
