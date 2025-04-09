using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace JerarquiaTaller.Logic
{
    internal class HierarchyParser
    {
        internal Registry open(string fileName)
        {
            XmlDocument documento = new XmlDocument();
            Registry salida = new Registry();
            try
            {                
                string auxFileName = System.IO.Path.Combine(AppContext.BaseDirectory,"data", fileName);
                documento.Load(auxFileName);
                foreach (XmlNode node in documento.ChildNodes)
                {
                    if(node.NodeType == XmlNodeType.Element && node.Name.Equals("root") )
                    {
                        foreach(XmlNode hijo in  node.ChildNodes)
                        {
                            if(hijo.NodeType == XmlNodeType.Element && hijo.Name.Equals("classes"))
                            {
                                salida.processXml(hijo);
                            }
                        }
                    }                        
                }                
            }
            catch (Exception e)
            {

            }
            return salida;
        }
    }
}
