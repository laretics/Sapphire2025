using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace JerarquiaTaller.Logic
{
    /// <summary>
    /// Contenedor de todas las referencias y clases registradas
    /// Es el objeto que puede crear la estructura.
    /// </summary>
    internal class Registry
    {
        internal Dictionary<string, ClassComponent> mcolClasses;

        internal Registry()
        {
            mcolClasses = new Dictionary<string, ClassComponent>();
        }

        internal void processXml(XmlNode node)
        {
            //Recorre todos los nodos de la estructura creando clases
            foreach (XmlNode child in node.ChildNodes)
            {
                if(child.NodeType == XmlNodeType.Element)
                {
                    if(child.Name.Equals("cc"))
                    {
                        //Registramos una clase
                        ClassComponent? componente = registerClass(child);
                        if(null!= componente )
                        {
                            if(mcolClasses.ContainsKey(componente.StorageId))
                            {
                                ClassComponent previo = mcolClasses[componente.StorageId];
                                MessageBox.Show(string.Format("An element with reference {0} and name {2} already exists. Please check reference of object {1} before retrying", componente.StorageId, componente.Name,previo.Name),"Duplicate index",MessageBoxButtons.OK);
                            }
                            else
                                mcolClasses.Add(componente.StorageId, componente);
                        }                            
                    }                    
                }
            }
        }

        internal ClassComponent? registerClass(XmlNode node)
        {
            ClassComponent? salida = null;
            if (null != node.Attributes)
            {
                XmlAttribute? idAtrib = node.Attributes["id"];
                XmlAttribute? nameAtrib = node.Attributes["name"];
                XmlAttribute? commentAtrib = node.Attributes["comment"];
                if(null==idAtrib)
                {
                    if (null != nameAtrib)
                        MessageBox.Show(string.Format("Class {0} has no reference", nameAtrib.Value), "Syntax error", MessageBoxButtons.OK);
                }
                if (null==nameAtrib)
                {
                    if (null != idAtrib)
                        MessageBox.Show(string.Format("Class id {0} has no name", idAtrib.Value), "Syntax error", MessageBoxButtons.OK);                    
                }
                if (null != idAtrib && null != nameAtrib)
                {
                    string? comment = null;
                    if (null != commentAtrib)
                        comment = commentAtrib.Value;
                    salida = new ClassComponent(idAtrib.Value, nameAtrib.Value, comment);
                    //Recorremos los hijos por si hay referencias o magnitudes
                    foreach (XmlNode child in node.ChildNodes)
                    {
                        if(child.NodeType == XmlNodeType.Element)
                        {
                            if(child.Name.Equals("rr"))
                            {
                                XmlAttribute? idRef = child.Attributes["id"];
                                XmlAttribute? nameRef = child.Attributes["name"];
                                XmlAttribute? commentRef = child.Attributes["comment"];
                                if(null != idRef && null != nameRef)
                                {
                                    if(mcolClasses.ContainsKey(idRef.Value))
                                    {
                                        comment = null;
                                        if(null != commentRef)
                                            comment = commentRef.Value;
                                        ReferenceInstance referencia = new ReferenceInstance(mcolClasses[idRef.Value], nameRef.Value, comment);
                                        salida.Children.Add(referencia);
                                    }
                                    else
                                    {
                                        MessageBox.Show(string.Format("Class {0} ({1}) references an object with key {2} wich could not be found on register. Please review it.", salida.Name, salida.StorageId, idRef.Value), "Null reference error", MessageBoxButtons.OK);
                                    }
                                }
                            }
                            else if (child.Name.Equals("magnitude"))
                            {
                                if(null!=child.Attributes)
                                {
                                    XmlAttribute? nameMag = child.Attributes["name"];
                                    XmlAttribute? minMag = child.Attributes["min"];
                                    XmlAttribute? maxmag = child.Attributes["max"];
                                    XmlAttribute? unitsMag = child.Attributes["units"];
                                    XmlAttribute? commentmag = child.Attributes["comment"];
                                    if (null == nameMag)
                                        MessageBox.Show(string.Format("Unknown magnitude in Class {1} ({2}) has not min value", salida.Name, salida.StorageId), "Null reference error", MessageBoxButtons.OK);
                                    else
                                    {
                                        float auxMin = float.MinValue;
                                        float auxMax = float.MaxValue;
                                        string units = "";
                                        if (null == minMag)
                                        {
                                            MessageBox.Show(string.Format("Magnitude {0} in Class {1} ({2}) has not min value",nameMag, salida.Name, salida.StorageId), "Null reference error", MessageBoxButtons.OK);
                                        }
                                        else
                                            float.TryParse(minMag.Value, out auxMin);
                                        if (null == maxmag)
                                        {
                                            MessageBox.Show(string.Format("Magnitude {0} in Class {1} ({2}) has not max value", nameMag, salida.Name, salida.StorageId), "Null reference error", MessageBoxButtons.OK);
                                        }
                                        else
                                            float.TryParse(maxmag.Value, out auxMax);
                                        if (null == unitsMag)
                                            MessageBox.Show(string.Format("Magnitude {0} in Class {1} ({2}) is adimensional", nameMag, salida.Name, salida.StorageId), "Null reference error", MessageBoxButtons.OK);
                                        else
                                            units = unitsMag.Value;

                                        comment = null;
                                        if (null != commentmag)
                                            comment = commentmag.Value;
                                        Magnitude magnitud = new Magnitude(nameMag.Value, auxMin, auxMax, units, comment);
                                        salida.Magnitudes.Add(magnitud);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return salida;
        }
    }
}
