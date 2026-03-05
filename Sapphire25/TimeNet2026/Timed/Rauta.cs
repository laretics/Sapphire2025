using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using TimeNet2026.Topo;

namespace TimeNet2026.Timed
{
    /// <summary>
    /// Rauta es un conjunto de planes de explotación asociados a una cabecera.
    /// </summary>
    public class Rauta
    {                                                          
        public Header Header { get; set; }
        internal Dictionary<string, Plan> mcolPlans;
        internal TopoStorage mvarParent;        
    
        public Rauta(TopoStorage parent)
        {
            Header = new Header();
            mcolPlans = new Dictionary<string, Plan>();
            mvarParent = parent;
        }
        public Dictionary<string,Plan> Plans { get => mcolPlans; }
        public TopoStorage Parent { get => mvarParent; }
    
        internal static Guid TopoStorageId(XNode root)
        {
            if( root is XElement element)
            {
                foreach(XElement hijo in element.Elements())
                {
					if (hijo.Name.LocalName == "info")
					{
						Header auxHeader = new Header();
						auxHeader.deserialize(hijo);
						return auxHeader.ParentId;
					}
				}
            }
            return Guid.Empty;
        }
    }
}
