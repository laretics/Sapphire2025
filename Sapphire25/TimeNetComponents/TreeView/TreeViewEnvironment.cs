using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using TimeNet2026.DBStorage;
using TimeNet2026.Production;
using TimeNet2026.Storage;
using TimeNet2026.Timed;
using TimeNet2026.Topo;

namespace TimeNetComponents.TreeView
{
    /// <summary>
    /// Esto es una clase contenedora de un árbol de nodos que contiene propiedades comunes y métodos de selección.
    /// </summary>
    public class TreeViewEnvironment
    {
		public TreeNode Root { get; private set; }

		public TreeViewEnvironment(TimeNetEnvironment tnEnvironment)
		{	
			if(null==tnEnvironment.TopoStorage)
				//No hay un almacenamiento seleccionado. El árbol se muestra entero
				Root = new TreeNode(this, TreeNode.NodeType.OnyxStorage, tnEnvironment);
			else
				//Tenemos un almacenamiento seleccionado. Modo normal de Tourmaline
				Root = new TreeNode(this, TreeNode.NodeType.TopoStorage, tnEnvironment);
		}

		internal List<TreeNode> Children(TreeNode element)
        {
            List<TreeNode> children = new List<TreeNode>();
			TimeNetEnvironment enviro = element.NetEnvironment;
			TreeNode nuevo;
			switch (element.Type)
			{
				case TreeNode.NodeType.OnyxStorage:
					foreach (TopoStorage topo in enviro.OnyxStorage.Storages.Values)
					{
						nuevo = new TreeNode(this, null, TreeNode.NodeType.TopoStorage);
						nuevo.NetEnvironment.TopoStorage = topo;
						children.Add(nuevo);
					}
					break;
				case TreeNode.NodeType.TopoStorage:
					children.Add(new TreeNode(this,element, TreeNode.NodeType.AxisCollection));
					children.Add(new TreeNode(this,element, TreeNode.NodeType.AsimilationCollection));
					if(null!=enviro.TopoStorage)
					{
						foreach (Rauta auxRauta in enviro.TopoStorage.ColRauta.Values)
						{
							nuevo = new TreeNode(this,element, TreeNode.NodeType.Rautatie);
							nuevo.NetEnvironment.Rauta = auxRauta;
							children.Add(nuevo);
						}
					}
					break;
				case TreeNode.NodeType.AxisCollection:
					if(null!=enviro.TopoStorage)
					{
						foreach(Axis eje in enviro.TopoStorage.ColAxis)
						{
							nuevo = new TreeNode(this,element, TreeNode.NodeType.Axis);
							nuevo.NetEnvironment.Axis = eje;
							children.Add(nuevo);
						}
					}
					break;
				case TreeNode.NodeType.AsimilationCollection:
					if(null!=enviro.TopoStorage)
					{
						foreach(Asimilation asimila in enviro.TopoStorage.ColAsimilations.Values)
						{
							nuevo = new TreeNode(this,element, TreeNode.NodeType.Asimilation);
							nuevo.NetEnvironment.Asimilation = asimila;
							nuevo.NetEnvironment.Rauta = enviro.Rauta;
							if (null!=enviro.Rauta && null!=enviro.Plan)
								nuevo.Url = $"/asimilationview/{enviro.TopoStorage.Header.Id}/{enviro.Rauta.Header.Id}/{enviro.Plan.Id}/{asimila.id}";														
							children.Add(nuevo);
						}
					}
					break;
				case TreeNode.NodeType.Asimilation:	//La asimilación devuelve las circulaciones asimiladas que la usan.
					if(null!=enviro.Asimilation && null!=enviro.Plan)
					{
						foreach(Circulation candidato in enviro.Plan.Circulations)
						{
							if(candidato.asimilation==enviro.Asimilation)
							{
								nuevo = new TreeNode(this,element, TreeNode.NodeType.Circulations);
								nuevo.NetEnvironment.Circulation = candidato;
								children.Add(nuevo);
							}
						}
					}
					break;

				case TreeNode.NodeType.Axis:
					if(null!=enviro.Axis)
					{
						foreach(Station estacion in enviro.Axis.Stations)
						{
							nuevo = new TreeNode(this,element, TreeNode.NodeType.Station);
							nuevo.NetEnvironment.Axis = enviro.Axis;
							nuevo.ContentId = estacion.id;
							children.Add(nuevo);
						}
					}
					break;
				case TreeNode.NodeType.Rautatie:
					if(null!=enviro.Rauta)
					{
						foreach(Plan plan in enviro.Rauta.Plans.Values)
						{
							nuevo = new TreeNode(this,element, TreeNode.NodeType.Plan);
							nuevo.NetEnvironment.Rauta = enviro.Rauta;
							nuevo.NetEnvironment.Plan = plan;
							children.Add(nuevo);
						}
					}
					break;
				case TreeNode.NodeType.Plan:
					if(null!=enviro.Plan && null!=enviro.Rauta && null!=enviro.TopoStorage)
					{
						children.Add(new TreeNode(this,element, TreeNode.NodeType.Circulations));
						//TODO: Añadir nodo de turnos de trabajo asociados.
					}
					break;
				case TreeNode.NodeType.Circulations:
					if(null!=enviro.Plan)
					{
						Dictionary<Asimilation, List<Circulation>> circulations = new Dictionary<Asimilation, List<Circulation>>();
						foreach (Circulation tren in enviro.Plan.Circulations)
						{
							if (null != tren.asimilation)
							{
								if (!circulations.ContainsKey(tren.asimilation))
									circulations.Add(tren.asimilation, new List<Circulation>());
								circulations[tren.asimilation].Add(tren);
							}
						}
						foreach (KeyValuePair<Asimilation, List<Circulation>> auxPar in circulations)
						{
							nuevo = new TreeNode(this,element, TreeNode.NodeType.Asimilation);
							nuevo.NetEnvironment.Rauta = enviro.Rauta;
							nuevo.NetEnvironment.Asimilation = auxPar.Key;
							nuevo.NetEnvironment.ViewAsimilation = auxPar.Key;
							children.Add(nuevo);							
						}
					}
					break;
			}
			return children;
		}
    }
}
