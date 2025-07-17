using Microsoft.EntityFrameworkCore;
using Sapphire2025Models;
using Sapphire2025Server.Models.Turnos;
using System.Diagnostics;
using System.Xml;

namespace Sapphire2025Server.Expert
{
    public class WorkSheetTemplateCollectionImporter
    {
        protected IConfiguration mvarConfiguration;
        protected string mvarName;
        protected string? mvarComment;
        protected Sapphire2025Server.Models.User? mvarAuthor;
        protected DateTime mvarStart;
        protected Guid mvarGuid;
        protected Guid? mvarInclude;
        protected byte mvarCollective;
        public WorkSheetTemplateCollectionImporter(IConfiguration config)
        {
            mvarConfiguration = config;
            mvarCollective = 0; //De momento sólo vamos con maquinistas.
            mvarInclude = null; //No hereda de nadie.
        }
        public async Task<string> ImportXML(XmlDocument document)
        {
            string salida = await importHeader(document);
            if (salida.Length > 0) return salida;
            salida = await isWorkSheetTemplateCollectionCompatible();
            if (salida.Length > 0) return salida;
            return await CreateWorkShiftTemplate(document);
        }

        private async Task<string> importHeader(XmlDocument document)
        {
            XmlElement? raiz = document.DocumentElement;
            if (null == raiz || !raiz.Name.Equals("plan"))
                return "No se ha encontrado el elemento padre <plan> en el documento a importar.";

            string? auxName = raiz.Attributes["name"]?.Value;
            if (null == auxName)
                return "El plan de explotación que está intentando dar de alta no tiene un nombre válido. Asigne un valor a 'name'.";
            else
                mvarName = auxName;

            mvarComment = raiz.Attributes["comment"]?.Value;

            DateTime? auxInicio = Sapphire2025Models.Common.parseSapphireDate(raiz.Attributes["start"]?.Value);
            if (null == auxInicio)
                return "No se ha especificado una fecha de comienzo para este proyecto de explotación. Debe usar el atributo 'start' con una fecha válida.";
            else
                mvarStart = (DateTime)auxInicio;

            string? auxAuthor = raiz.Attributes["author"]?.Value;
            if (null == auxAuthor)
                return "No se ha especificado un autor para este documento. Es necesario aportar la identificación (CF) de la persona que ha creado esta colección de turnos en el sistema.";

            string? auxInclude = raiz.Attributes["include"]?.Value;
            if(null!=auxInclude)
            {
                //Este plan hereda de otro. Tenemos que averiguar su ID.
                using (DataStorage almacen = new DataStorage(mvarConfiguration))
                {
                    WorkShiftTemplateCollection? col = await almacen.WorkShiftTemplateCollections
                        .Where(x => null!=x.Name && x.Name.Equals(auxInclude))
                        .FirstOrDefaultAsync();
                    if (null == col)
                        return string.Format("El plan de explotación {0} hace referencia a otro plan llamado {1} del que no se encuentra ninguna referencia en la base de datos.", mvarName, auxInclude);
                    else
                        mvarInclude = col.Id;
                }
            }
            using (DataStorage almacen = new DataStorage(mvarConfiguration))
            {
                Models.User? auxUser = await almacen.Users.Where(x => x.CF.Equals(auxAuthor)).FirstOrDefaultAsync();
                if (null == auxUser)
                    return string.Format("El usuario con CF {0} no existe en el sistema. Por favor, aporte un usuario válido y con permisos de administración.", auxAuthor);
                else
                    mvarAuthor = auxUser;
            }
            mvarGuid = Guid.NewGuid(); //Una vez superados los obstáculos, se puede generar el guid.
            return string.Empty;
        }
        private async Task<string> isWorkSheetTemplateCollectionCompatible()
        {
            using (DataStorage almacen = new DataStorage(mvarConfiguration))
            {
                WorkShiftTemplateCollection? col = await almacen.WorkShiftTemplateCollections.Where(x => x.Name.Equals(mvarName)).FirstOrDefaultAsync();
                if (null != col)
                    return string.Format("No se puede dar de alta otro plan con el mismo nombre {0} que uno ya existente.", mvarName);
                col = await almacen.WorkShiftTemplateCollections.Where(x => x.Begin.Equals(mvarStart)).FirstOrDefaultAsync();
                if (null != col)
                    return string.Format("Ya existe un plan de explotación con la misma fecha: {0:dd-MM-yy}.", mvarStart);
            }
            return string.Empty;
        }
        private async Task<string> CreateWorkShiftTemplate(XmlDocument doc)
        {
            Debug.Assert(null != mvarAuthor);
            using (DataStorage almacen = new DataStorage(mvarConfiguration))
            {
                WorkShiftTemplateCollection padre = new WorkShiftTemplateCollection();
                padre.Id = mvarGuid;
                padre.Begin = mvarStart;
                padre.Name = mvarName;
                padre.Comment = mvarComment;
                padre.Collective = mvarCollective;
                padre.Include = mvarInclude;
                padre.Owner = mvarAuthor.guid;                
                almacen.WorkShiftTemplateCollections.Add(padre);
                foreach (XmlNode seccion in doc.ChildNodes)
                {
                    if(seccion.NodeType== XmlNodeType.Element)
                    {
                        if(seccion.Name.Equals("plan"))
                        {
                            foreach(XmlNode hijo in seccion.ChildNodes)
                            {
                                switch (hijo.Name)
                                {
                                    case "inactive": importDescansos(hijo, almacen); break;
                                    case "active": importTrabajos(hijo, almacen); break;
                                }
                            }
                        }
                    }
                }
                if (await almacen.SaveChangesAsync() < 1)
                    return "Por algún motivo no se ha guardado ningún cambio en la base de datos.";
            }
            return "";
        }
        private void importDescansoAndTrabajoCommon(WorkShiftTemplate template, XmlNode nodo)
        {
            template.Id = Guid.NewGuid();
            template.Name = nodo.Attributes["name"].Value;
            template.Comment = nodo.Attributes["comment"]?.Value;
            template.Color = nodo.Attributes["col"]?.Value;
            template.BgColor = nodo.Attributes["bgcol"]?.Value;
            template.StripeColor = nodo.Attributes["stcol"]?.Value;
            template.CoorX = -1;
            template.CoorY = -1;
            string? auxCoordinates = nodo.Attributes["ord"]?.Value;
            if (null != auxCoordinates && auxCoordinates.Length > 0)
            {
                string[] coordinates = auxCoordinates.Split(",");
                int coordenada = -1;
                int.TryParse(coordinates[0], out coordenada);
                template.CoorX = coordenada;
                coordenada = -1;
                int.TryParse(coordinates[1], out coordenada);
                template.CoorY = coordenada;
            }
            template.Parent = mvarGuid;
        }
        private void importDescansos(XmlNode node, DataStorage almacen)
        {
            foreach (XmlNode descanso in node.ChildNodes)
            {
                if(descanso.Name.Equals("ws"))             
                {
                    if(null!=descanso.Attributes && null != descanso.Attributes["name"])
                    {
                        WorkShiftTemplate auxDescanso = new WorkShiftTemplate();
                        auxDescanso.Active = false;
                        importDescansoAndTrabajoCommon(auxDescanso, descanso);                        

                        almacen.WorkShiftTemplates.Add(auxDescanso);
                    }                    
                }
            }
        }
        private void importTrabajos(XmlNode node, DataStorage almacen)
        {
            foreach(XmlNode trabajo in node.ChildNodes)
            {
                if(trabajo.Name.Equals("ws"))
                {
                    TimeSpan? auxComienzo = Common.parseSapphireTimeSpan(trabajo.Attributes["start"]?.Value);
                    TimeSpan? auxDuracion = Common.parseSapphireTimeSpan(trabajo.Attributes["duration"]?.Value);
                    if (null != trabajo.Attributes && null != trabajo.Attributes["name"] && null!=auxComienzo && null!=auxDuracion)
                    {
                        WorkShiftTemplate auxTrabajo = new WorkShiftTemplate();
                        auxTrabajo.Active = true;
                        importDescansoAndTrabajoCommon(auxTrabajo, trabajo);
                        auxTrabajo.StartTime = (TimeSpan)auxComienzo;
                        auxTrabajo.Duration = (TimeSpan)auxDuracion;
                        string? auxDepot = trabajo.Attributes["depot"]?.Value;
                        auxTrabajo.Att = (null != auxDepot && auxDepot.ToUpper().Contains("T"));
                        auxTrabajo.Id = Guid.NewGuid();

                            string? auxWeek = trabajo.Attributes["week"]?.Value;
                        auxTrabajo.PerWeek = parseWeekDays(auxWeek);
                        almacen.WorkShiftTemplates.Add(auxTrabajo);
                        importWorkSheetContents(trabajo,auxTrabajo, almacen);
                    }                       
                }
            }
        }
        private byte parseWeekDays(string? rhs)
        {
            if (null == rhs)
                return 0xff; //Cualquier día de la semana.
            else
            {
                string cadenaWeek = rhs.Trim().ToUpper();
                if (cadenaWeek.Equals("FFF"))
                    return 1 | 64 | 128; //Sábados domingos y festivos.
                else if (cadenaWeek.Equals("LAB"))
                    return 2 | 4 | 8 | 16 | 32; //Laborables.
                else
                {
                    byte salida = 0;
                    if (cadenaWeek.Contains('L')) salida |= 2;
                    if (cadenaWeek.Contains('M')) salida |= 4;
                    if (cadenaWeek.Contains('X')) salida |= 8;
                    if (cadenaWeek.Contains('J')) salida |= 16;
                    if (cadenaWeek.Contains('V')) salida |= 32;
                    if (cadenaWeek.Contains('S')) salida |= 64;
                    if (cadenaWeek.Contains('D')) salida |= 1;
                    if (cadenaWeek.Contains('F')) salida |= 128;
                    return salida;
                }
            }
        }
        private void importWorkSheetContents(XmlNode nodo, WorkShiftTemplate parent,DataStorage almacen)
        {
            foreach(XmlNode hijo in nodo.ChildNodes)
            {
                TimeSpan? auxBegin;
                TimeSpan? auxEnd;
                switch (hijo.Name)
                {
                    case "train":
                        auxBegin = Common.parseSapphireTimeSpan(hijo.Attributes["start"]?.Value);
                        auxEnd = Common.parseSapphireTimeSpan(hijo.Attributes["end"]?.Value);
                        string? auxName = hijo.Attributes["id"]?.Value;
                        if (null != auxBegin && null != auxEnd && null != auxName)
                        {
                            WorkShiftContent tren = createWorkshiftContent((TimeSpan)auxBegin, (TimeSpan)auxEnd,parent.Id, 1);
                            bool auxDisc = (null != hijo.Attributes["disc"] && hijo.Attributes["disc"].Value.ToUpper().Contains("T"));
                            tren.Discrectional = auxDisc;
                            tren.TrainId = auxName;
                            almacen.WorkShiftContents.Add(tren);                            
                        }
                        break;
                    case "depot":
                        auxBegin = Common.parseSapphireTimeSpan(hijo.Attributes["start"]?.Value);
                        auxEnd = Common.parseSapphireTimeSpan(hijo.Attributes["end"]?.Value);
                        bool auxForeign = (null != hijo.Attributes["foreign"] && hijo.Attributes["foreign"].Value.ToUpper().Contains("T"));
                        if (null != auxBegin && null != auxEnd)
                        {
                            WorkShiftContent deposito = createWorkshiftContent((TimeSpan)auxBegin, (TimeSpan)auxEnd, parent.Id, 0);
                            deposito.Foreign = auxForeign;
                            almacen.WorkShiftContents.Add(deposito);
                        }
                        break;
                }
            }

        }
        private WorkShiftContent createWorkshiftContent(TimeSpan begin, TimeSpan end, Guid parent, byte contentType)
        {
            WorkShiftContent salida = new WorkShiftContent();
            salida.Begin = begin;
            salida.Duration = (end.Subtract(begin));
            salida.Id = Guid.NewGuid();
            salida.Parent = parent;
            salida.ParentCollection = mvarGuid;
            salida.ContentType = contentType;
            return salida;
        }
    }
}
