using Microsoft.EntityFrameworkCore;
using Sapphire2026.Data;
using Sapphire2026.Data.Models;
using Sapphire2026.Data.Models.Turnos;
using System.Security.AccessControl;
using System.Xml;

namespace Sapphire2025Server.Expert
{
    public class AgentsListImporter
    {
        protected IConfiguration mvarConfiguration;
        public AgentsListImporter(IConfiguration config)
        {
            mvarConfiguration = config;
        }
        public async Task<string> ImportXML(XmlDocument document)
        {
            foreach (XmlNode elemento in document.ChildNodes)
            {
                if(elemento.NodeType== XmlNodeType.Element && elemento.Name.Equals("agentslist"))
                {
                    foreach(XmlNode hijo in elemento.ChildNodes)
                    {
						if (hijo.NodeType == XmlNodeType.Element && hijo.Name.Equals("list"))
						{
							string salida = await auxImportNode(hijo);
							if (!salida.Equals(string.Empty)) return salida;
						}
					}
                }
            }
            return string.Empty;
        }

        private async Task<string> auxImportNode(XmlNode rhs)
        {
            if(null!=rhs.Attributes)
            {
                string? name = rhs.Attributes["name"]?.Value;
                string? final = rhs.Attributes["final"]?.Value;
                string? comments = rhs.Attributes["comments"]?.Value;
                if (null == name || name.Length < 1)
                    return "Una de las listas de Agentes no tiene un nombre válido.";
                ExpertAgentsListView nueva = new ExpertAgentsListView();
                if (!await RemoveList(nueva.Name)) return string.Format("El programa no pudo eliminar la antigua lista de agentes con nombre {0}.", name);
                nueva.Name = name;
                nueva.Id = Guid.NewGuid();
                nueva.Comments = comments;
                nueva.Final = (null != final && final.ToUpper().StartsWith("T"));
                mvarSequence = 0; //Inicio la secuencia de presentación en la tabla
                using (DataStorage almacen = new DataStorage(mvarConfiguration))
                {
                    almacen.ExpertAgentsListViews.Add(nueva);
                    foreach (XmlNode hijo in rhs.ChildNodes)
                    {
                        if(hijo.NodeType== XmlNodeType.Element)
                        {
                            string result = await auxImportAgentRegister(hijo, almacen,nueva.Id);
                            if (string.Empty != result)
                                return result;
                        }
                    }
                    await almacen.SaveChangesAsync();
                }
                return string.Empty;
            }
            return "Una de las listas de Agentes no contiene datos válidos.";
        }

        private int mvarSequence; //Secuencia de presentación para las tablas.
        /// <summary>
        /// Importa uno de los nodos de una lista de Agentes, incluyendo includes y espacios en blanco
        /// </summary>
        /// <param name="rhs">Nodo a importar</param>
        /// <param name="almacen">Referencia al almacén</param>
        /// <returns></returns>
        private async Task<string> auxImportAgentRegister(XmlNode rhs, DataStorage almacen, Guid parentId)
        {
            switch(rhs.Name)
            {
                case "agent": return await auxImportAgent(rhs, almacen, parentId);
                case "space": return auxImportSpace(almacen,parentId);
                case "include": return await auxImportInclude(rhs, almacen,parentId);
                default: return ""; //Ignoramos los elementos que tengan otros nombres.
            }            
        }

        private async Task<string> auxImportAgent(XmlNode rhs, DataStorage almacen, Guid parentId)
        {
            if (null == rhs.Attributes) return "Detectado un registro de un Agente ( <agent/> sin parámetros)";
            string? auxCF = rhs.Attributes["cf"]?.Value;
            string? auxName = rhs.Attributes["name"]?.Value;
            if(null==auxCF || null==auxName)
            {
                if (null == auxName) return "Uno de los registros de Agente no tiene un carnet ferroviario (cf) o un nombre (name) válidos.";
                    return string.Format("El agente {0} no tiene un carnet ferroviario (cf) válido.", auxName);
            }
            User? auxAgente = await almacen.Users.Where(x => x.CF == auxCF).FirstOrDefaultAsync();
            if (null == auxAgente) return string.Format("El agente {0} con CF número {1} no existe en la tabla de usuarios registrados.", auxName, auxAgente);

            ExpertAgentListRecord registro = new ExpertAgentListRecord();
            registro.Order = mvarSequence++;
            registro.Id = Guid.NewGuid();
            registro.ParentId = parentId;
            registro.Type = 0; //Agente.
            registro.ElementId = auxAgente.guid;
            almacen.ExpertAgentListRecords.Add(registro);
            return string.Empty;
        }
        private async Task<string> auxImportInclude(XmlNode rhs, DataStorage almacen, Guid parentId)
        {
            if (null == rhs.Attributes) return "Detectado un elemento de inclusión ( <include/> sin parámetros)";
            string? listaName = rhs.Attributes["id"]?.Value;
            if (null == listaName) return "Uno de los elementos de inclusión tiene un nombre que no es válido";
            ExpertAgentsListView? auxLista = await almacen.ExpertAgentsListViews.Where(x => x.Name == listaName).FirstOrDefaultAsync();
            if (null == auxLista) return string.Format("Un elemento de inclusión hace referencia a una lista llamada {0} que no está definida en el archivo. Asegúrese de que la lista referida se ha definido ANTES.", listaName);

            ExpertAgentListRecord registro = new ExpertAgentListRecord();
            registro.Order = mvarSequence++;
            registro.Id = Guid.NewGuid();
            registro.ParentId = parentId;
            registro.Type = 2; //Lista o Include.
            registro.ElementId = auxLista.Id;
            almacen.ExpertAgentListRecords.Add(registro);
            return string.Empty;
        }
        private string auxImportSpace(DataStorage almacen, Guid parentId)
        {
            ExpertAgentListRecord registro = new ExpertAgentListRecord();
            registro.Order = mvarSequence++;
            registro.Id = Guid.NewGuid();
            registro.ParentId = parentId;
            registro.Type = 1; //Separador.
            registro.ElementId = Guid.Empty; //Esto no era necesario, pero da igual.
            almacen.ExpertAgentListRecords.Add(registro);
            return string.Empty;
        }

        /// <summary>
        /// No puedo tener dos tablas de Agentes con el mismo nombre. La forma discreta de llevarlo es
        /// eliminar la posible tabla existente cuando pretenda darla de alta.
        /// Esta función es precisamente para eso.
        /// </summary>
        /// <param name="id">Nombre de la tabla de Agentes a eliminar</param>
        /// <returns>True si fue bien. False en caso contrario.</returns>
        private async Task<bool> RemoveList(string id)
        {
            try
            {
                using (DataStorage almacen = new DataStorage(mvarConfiguration))
                {
                    ExpertAgentsListView? auxLista = await almacen.ExpertAgentsListViews.Where(x => x.Name.Equals(id)).FirstOrDefaultAsync();
                    if (null != auxLista)
                    {
                        Guid parentId = auxLista.Id;
                        List<ExpertAgentListRecord> registros = await almacen.ExpertAgentListRecords.Where(x => x.ParentId == parentId).ToListAsync();
                        almacen.RemoveRange(registros);
                        await almacen.SaveChangesAsync();                        
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
