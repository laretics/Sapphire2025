using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
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
        public IEnumerable<Plan> Plans { get => mcolPlans.Values; }

        public Rauta(XmlNode root, TopoStorage parent):this(parent)
        {
            foreach(XmlNode hijo in root.ChildNodes)
            {
                switch(hijo.Name)
                {
                    case "info":
                        Header = new Header();
                        Header.deserialize(hijo);
                        break;
                    case "plans":
                        deserializePlans(hijo);                        
                        break;
                }
            }
        }
        internal void deserializePlans(XmlNode root)
        {
            foreach(XmlNode hijo in root.ChildNodes)
            {
                if(hijo.Name=="plan")
                {
                    Plan nuevo = new Plan(hijo, mvarParent);
                    mcolPlans.Add(nuevo.mvarName, nuevo);
                }
            }
        }
    
        internal static Guid TopoStorageId(XmlNode root)
        {
            foreach(XmlNode hijo in root.ChildNodes)
            {
                if(hijo.Name=="info")
                {
                    Header auxHeader = new Header();
                    auxHeader.deserialize(hijo);
                    return auxHeader.ParentId;
                }
            }
            return Guid.Empty;
        }
    }
}
