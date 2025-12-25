using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimeNet2026.Timed;

namespace TimeNetComponents.TreeView
{
    internal class RautaNode : TreeNode
    {
        public Rauta content { get; private set; }
        public RautaNode(Rauta content)
        {
            this.content = content;
        }
        public override string Name => string.Format("{0}({1},{2}) {3} planes", 
            content.Header.Name, 
            content.Header.Version, 
            content.Header.Author,
            content.Plans.Count());
        public override string SvgIcon => "<svg xmlns=\"http://www.w3.org/2000/svg\" height=\"16px\" viewBox=\"0 0 24 24\" width=\"16px\" fill=\"gray\"><path d=\"M0 0h24v24H0V0z\" fill=\"none\"/><path d=\"M2 20h20v-4H2v4zm2-3h2v2H4v-2zM2 4v4h20V4H2zm4 3H4V5h2v2zm-4 7h20v-4H2v4zm2-3h2v2H4v-2z\"/></svg>";

        public override List<TreeNode> Children
        {
            get
            {
                List<TreeNode> salida = new List<TreeNode>();
                foreach(Plan elemento in content.Plans)
                {
                    PlanNode nuevo = new PlanNode(elemento);
                    salida.Add(nuevo);
                }               
                return salida;
            }
        }

    }
}
