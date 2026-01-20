using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using TimeNet2026.Timed;
using TimeNet2026.Topo;

namespace TimeNetComponents.TreeView
{
    /// <summary>
    /// Esto es una clase contenedora de un árbol de nodos que contiene propiedades comunes y métodos de selección.
    /// </summary>
    public class TreeViewEnvironment
    {
        internal List<TreeNode> Children(TreeNode element)
        {
            List<TreeNode> children = new List<TreeNode>();
			if (element.GetType() == typeof(OnyxStorageNode))
			{
				OnyxStorageNode onice = (OnyxStorageNode)element;
				foreach (TopoStorage topo in onice.content.Storages.Values)
				{
					TopoStorageNode hijo = new TopoStorageNode(this,topo);
					children.Add(hijo);
				}
			}
			else if (element.GetType()==typeof(TopoStorageNode))
            {
                TopoStorageNode tsNode = (TopoStorageNode)element;
				children.Add(new AxisCollectionNode(this,tsNode.TopoStorage));
				children.Add(new AsimilationCollectionNode(this,tsNode.TopoStorage));
				foreach (Rauta auxRauta in tsNode.TopoStorage.ColRauta.Values)
					children.Add(new RautaNode(this,auxRauta, tsNode.TopoStorage));
			}
            else if (element.GetType() == typeof(AxisCollectionNode))
            {
                AxisCollectionNode acNode = (AxisCollectionNode)element;
				foreach (Axis eje in acNode.TopoStorage.ColAxis)
				{
					AxisNode nuevo = new AxisNode(this,eje, acNode.TopoStorage);
					children.Add(nuevo);
				}
			}
			else if (element.GetType() == typeof(AsimilationCollectionNode))
            {
                AsimilationCollectionNode ascNode = (AsimilationCollectionNode)element;
				foreach (Asimilation asimila in ascNode.TopoStorage.ColAsimilations.Values)
				{
					AsimilationNode nuevo = new AsimilationNode(this,asimila, ascNode.TopoStorage, ascNode.Rauta, ascNode.Plan);
					children.Add(nuevo);
				}
			}
			else if (element.GetType() == typeof(AxisNode))
			{
				AxisNode axisNode = (AxisNode)element;
				foreach (Station estacion in axisNode.Axis.Stations)
				{
					StationNode nuevo = new StationNode(this,axisNode.TopoStorage, axisNode.Axis, estacion);
					children.Add(nuevo);
				}
			}
			else if (element.GetType() == typeof(AsimilationNode))
			{
				AsimilationNode asim = (AsimilationNode)element;
				if (null != asim.Plan && null != asim.Rauta)
				{
					foreach (Circulation candidato in asim.Plan.Circulations)
						children.Add(new CirculationNode(this,asim.TopoStorage, asim.Rauta, asim.Plan, candidato));
				}
			}
			else if (element.GetType() == typeof(RautaNode))
			{
				RautaNode rautaNode = (RautaNode)element;
				foreach (Plan elemento in rautaNode.Rauta.Plans.Values)
				{
					PlanNode nuevo = new PlanNode(this,elemento, rautaNode.TopoStorage, rautaNode.Rauta);
					children.Add(nuevo);
				}
			}
			else if (element.GetType() == typeof(PlanNode))
			{
				PlanNode planNode = (PlanNode)element;
				children.Add(new CirculationsNode(this, planNode.Plan, planNode.TopoStorage, planNode.Rauta, true));
			}
			else if (element.GetType() == typeof(CirculationsNode))
			{
				CirculationsNode circa = (CirculationsNode)element;
				if (circa.foldered)
				{
					Dictionary<Asimilation, List<Circulation>> circulations = new Dictionary<Asimilation, List<Circulation>>();
					foreach (Circulation tren in circa.Plan.Circulations)
					{
						if (null != tren.asimilation)
						{
							if (!circulations.ContainsKey(tren.asimilation))
								circulations.Add(tren.asimilation, new List<Circulation>());
							circulations[tren.asimilation].Add(tren);
						}
					}
					foreach (KeyValuePair<Asimilation, List<Circulation>> auxPar in circulations)
						children.Add(new AsimilationNode(this,auxPar.Key, circa.TopoStorage, circa.Rauta, circa.Plan));
				}
				else
				{
					foreach (Circulation tren in circa.Plan.Circulations)
						children.Add(new CirculationNode(this, circa.TopoStorage, circa.Rauta, circa.Plan, tren));
				}

			}
				return children;
		}
    }
}
