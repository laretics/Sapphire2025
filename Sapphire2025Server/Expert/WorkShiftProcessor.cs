using Sapphire2025Models.Expert;
using Sapphire2025Server.Models.Turnos;
using Sapphire2025Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.Json;
using Org.BouncyCastle.Crypto.Operators;
using Sapphire2025Models.Expert.WorkshiftTemplates;
using System.Security.Cryptography.Xml;

namespace Sapphire2025Server.Expert
{
    public class WorkShiftProcessor
    {
        private IConfiguration mvarConfig; //Objeto de configuración para crear accesos a la base de datos.
        public WorkShiftProcessor(IConfiguration config)
        {
            mvarConfig = config;
        }

        public async Task<List<WorkShiftTemplateCollectionModel>> Plans()
        {
            List<WorkShiftTemplateCollectionModel> salida = new List<WorkShiftTemplateCollectionModel>();
            using(DataStorage almacen = new DataStorage(mvarConfig))
            {
                List<WorkShiftTemplateCollection> origenes = await almacen.WorkShiftTemplateCollections
                    .OrderByDescending(x => x.Begin)
                    .ToListAsync();
                foreach(WorkShiftTemplateCollection origen in origenes)
                {
                    WorkShiftTemplateCollectionModel destino = new WorkShiftTemplateCollectionModel();
                    destino.Id = origen.Id;
                    destino.Begin = origen.Begin;
                    destino.Name = origen.Name;
                    destino.Comment = origen.Comment;
                    destino.Collective = origen.Collective;
                    destino.Owner = origen.Owner == null ? Guid.Empty : (Guid)origen.Owner;
                    salida.Add(destino);
                }
            }
            return salida;
        }

        public async Task<Guid> HeaderPlan(DateTime date, byte collective)
        {
            using (DataStorage almacen = new DataStorage(mvarConfig))
            {
                WorkShiftTemplateCollection? candidato = await almacen.WorkShiftTemplateCollections
                    .OrderByDescending(x => x.Begin)
                    .Where(x => x.Begin <= date && x.Collective == collective)
                    .FirstOrDefaultAsync();
                if (null != candidato) return candidato.Id;
            }
            return Guid.Empty;
        }

        /// <summary>
        /// Obtiene el plan de explotación para un día concreto de la semana.
        /// </summary>
        /// <param name="id">Id del plan de explotación</param>
        /// <param name="actives">Recupera sólo los turnos de trabajo</param>
        /// <param name="inactives">Recupera sólo los turnos de descanso</param>
        /// <param name="dayOfWeek">Días de la semana, en formato flag (1,2,4,8,16...)</param>
        /// <returns></returns>
        public async Task<WorkShiftTemplateCollectionModel?> Plan(Guid id)
        {
            WorkShiftTemplateCollectionModel? salida = null;
            WorkShiftTemplateCollectionModel? included = null ;
            using (DataStorage almacen = new DataStorage(mvarConfig))
            {
                WorkShiftTemplateCollection? candidato = await almacen.WorkShiftTemplateCollections.
                    Where(x => x.Id == id).FirstOrDefaultAsync();
                if(null!=candidato)
                {
                    User? usuario = await almacen.Users
                        .Where(x => x.CF.Equals(candidato.Owner)).FirstOrDefaultAsync();
                    if (null!=candidato.Include)
                    {
                        Guid auxId = (Guid)candidato.Include;
                        included = await Plan(auxId);
                    }                       
                    salida = new WorkShiftTemplateCollectionModel();
                    salida.Id = id;                    
                    salida.Name = candidato.Name;
                    salida.Comment = candidato.Comment;
                    salida.Collective = candidato.Collective;
                    salida.Begin = candidato.Begin;
                    salida.Owner = null == usuario ? Guid.Empty : usuario.guid;                    
                }
            }
            if (null != salida)
            {
                salida.Templates = await Templates(id);

                if (null != included && null != included.Templates)
                {
                    ///Creo que con esto tengo la herencia, pero hay que comprobarlo.
                    foreach (WorkShiftTemplateModel template in included.Templates)
                        salida.Add(template,false);
                }
            }                                                        
            return salida;
        }

        #region Festivos
        public async Task<HashSet<DateTime>> NextFestives(DateTime firstDay)
        {
            using (DataStorage almacen = new DataStorage(mvarConfig))
            {
                List<Festive> auxColeccion = await almacen.Festives
                    .Where(x => x.Date >= firstDay)
                    .ToListAsync();
                HashSet<DateTime> salida = new HashSet<DateTime>();
                foreach (Festive elemento in auxColeccion)
                    salida.Add(elemento.Date);
                return salida;
            }
        }
        public async Task<bool> SetFestive(DateTime rhs, bool value)
        {
            try
            {
				bool currentValue = await IsFestive(rhs);
				if (value != currentValue)
				{
					using (DataStorage almacen = new DataStorage(mvarConfig))
					{
						Festive? elemento;
						if (value && !currentValue)
						{
							elemento = new Festive();
							elemento.Date = rhs;
							almacen.Festives.Add(elemento);
						}
						else if (!value && currentValue)
						{
							elemento = await almacen.Festives
									.Where(x => x.Date == rhs)
									.FirstOrDefaultAsync();
							if (null != elemento)
								almacen.Remove(elemento);
						}
						await almacen.SaveChangesAsync();
					}
				}
			}
            catch (Exception ex)
            {
                return false;
            }
            return true;
        }
        public async Task<bool> IsFestive(DateTime rhs)
        {
            using (DataStorage almacen = new DataStorage(mvarConfig))
            {
                Festive? elemento = await almacen.Festives
                    .Where(x => x.Date == rhs)
                    .FirstOrDefaultAsync();
                return (null != elemento);
            }
        }
        #endregion Festivos

        #region Rodajas de tiempo
        /// <summary>
        /// Obtiene un objeto compuesto con las asignaciones de planes correspondientes a los días solicitados
        /// Se usa para representar calendarios de asignaciones (gráficos).
        /// </summary>
        /// <param name="begin">Fecha de inicio</param>
        /// <param name="dayCount">Número de días a devolver</param>
        /// <returns></returns>
        public async Task<PlansYearSlice> PlansTimeSlice(DateTime begin, int dayCount)
        {
            PlansYearSlice salida = new PlansYearSlice(dayCount,begin);
            using (DataStorage almacen = new DataStorage(mvarConfig))
            {
                WorkShiftTemplateCollection? auxPrimerPlan = null;
                Guid lastPlanId = Guid.Empty;
                WorkShiftTemplateCollectionModel? auxPlan = null;
                //Planes.
                for (int i = 0;i< dayCount;i++)
                {
                    DateTime auxFecha = begin.AddDays(dayCount);
                    auxPrimerPlan = await almacen.WorkShiftTemplateCollections
                    .Where(x => x.Begin <= auxFecha)
                    .OrderBy(x => x.Begin)
                    .LastOrDefaultAsync();
                    if(null!= auxPrimerPlan) 
                    {
                        if (auxPrimerPlan.Id != lastPlanId)
                        {
                            lastPlanId = auxPrimerPlan.Id;
                            auxPlan = await Plan(lastPlanId);
                        }
                        if(null!=auxPlan)
                            salida.SetPlan(i, auxPlan);
                    }
                }
                //Festivos.
                List<Festive> festives = await almacen.Festives
                   .Where(x => x.Date >= salida.InitialDate && x.Date <= salida.FinalDate)
                   .ToListAsync();
                foreach(Festive festivo in festives)
                    salida.SetFestive(festivo.Date, true);              
            }
            return salida;
        }

        #endregion Rodajas de tiempo

        #region Listas de Agentes
        //Aunque no debería estar aquí, por funcionalidad, al ser algo relacionado con los turnos, lo pongo en este objeto

        public async Task<List<string>> AgentsListsCatalog()
        {
            List<string> salida = new List<string>();
            using (DataStorage almacen = new DataStorage(mvarConfig))
            {
                List<ExpertAgentsListView> auxVistas = await almacen.ExpertAgentsListViews
                    .Where(x => x.Final).ToListAsync();
                foreach (ExpertAgentsListView vista in auxVistas)
                    salida.Add(vista.Name);
            }
            return salida;
        }
        public async Task<AgentsViewContainer> AgentsViewList(string name)
        {
            using (DataStorage almacen = new DataStorage(mvarConfig))
            {
                ExpertAgentsListView? auxVista = await almacen.ExpertAgentsListViews
                    .Where(x => x.Name.Equals(name))
                    .FirstOrDefaultAsync();
                if (null != auxVista)
                {
                    AgentsViewContainer salida = await AgentsViewList(auxVista.Id);
                    return salida;
                }
            }
            return new AgentsViewContainer();
        }
        public async Task<AgentsViewContainer> AgentsViewList(Guid id)
        {
            AgentsViewContainer salida = new AgentsViewContainer();
            salida.ShowHeader = false;
            salida.RegisterCollection = new List<AgentsViewRegisterModel>();
            using (DataStorage almacen = new DataStorage(mvarConfig))
            {
                ExpertAgentsListView? auxVista = await almacen.ExpertAgentsListViews
                    .Where(x => x.Id.Equals(id))
                    .FirstOrDefaultAsync();
                if(null!=auxVista)
                {
                    salida.Name = auxVista.Name;
                    List<ExpertAgentListRecord> auxContenido = await almacen.ExpertAgentListRecords
                        .Where(x => x.ParentId == id)
                        .OrderBy(x => x.Order)
                        .ToListAsync();
                    foreach (ExpertAgentListRecord elemento in auxContenido)
                    {
                        switch (elemento.Type)
                        {
                            case 0: //Agente                                
                                User? usuario = await almacen.Users
                                    .Where(x => x.Id == elemento.ElementId.ToString())
                                    .FirstOrDefaultAsync();
                                if(null!=usuario)
                                {
                                    AgentsViewAgent registroAgente = new AgentsViewAgent();
                                    registroAgente.CF = usuario.CF;
                                    registroAgente.Name = usuario.UserName??"??";
                                    registroAgente.Id = usuario.guid;
                                    salida.RegisterCollection?.Add(registroAgente);
                                }
                                break;
                            case 1: //Separador
                                AgentsViewSpace espacio = new AgentsViewSpace();
                                salida.RegisterCollection?.Add(espacio);
                                break;
                            case 2: //Sub-Lista
                                AgentsViewContainer subConjunto = await AgentsViewList(elemento.ElementId);
                                if(subConjunto.RegisterCollection?.Count>0)
                                {
                                    AgentsViewContainer subLista = new AgentsViewContainer();
                                    subLista.RegisterCollection = subConjunto.RegisterCollection;
                                    subLista.Name = subConjunto.Name;
                                    subLista.Show = true;
                                    subLista.ShowHeader = true;
                                    salida.RegisterCollection?.Add(subLista);
                                }
                                break;
                        }
                    }
                }
            }
            return salida;
        }

        #endregion Listas de Agentes

        protected async Task<List<WorkShiftTemplateModel>> Templates(Guid parent)
        {
            List<WorkShiftTemplateModel> salida = new List<WorkShiftTemplateModel>();

            using (DataStorage almacen = new DataStorage(mvarConfig))
            {
                List<WorkShiftTemplate> colTemplates = await almacen.WorkShiftTemplates
                    .Where(x => x.Parent == parent)
                    .ToListAsync();
                await auxAddTemplatesToList(salida, colTemplates);                
            }
            return salida;
        }

        private async Task auxAddTemplatesToList(List<WorkShiftTemplateModel> content, List<WorkShiftTemplate>origin)
        {
            foreach (WorkShiftTemplate auxPlantilla in origin)
            {
                WorkShiftTemplateModel? nuevo = null;
                if (!auxPlantilla.Active)
                {
                    nuevo = new RestTemplateModel();
                }
                else
                {
                    if (auxPlantilla.Att) //Depósito
                        nuevo = new AttTemplateModel();
                    else
                        nuevo = new WorkTemplateModel();
                }
                if (null != nuevo)
                {
                    nuevo.Name = auxPlantilla.Name;
                    nuevo.comment = auxPlantilla.Comment;
                    nuevo.Color = auxPlantilla.Color;
                    nuevo.BgColor = auxPlantilla.BgColor;
                    nuevo.StripeColor = auxPlantilla.StripeColor;
                    nuevo.CoorX = auxPlantilla.CoorX;
                    nuevo.CoorY = auxPlantilla.CoorY;
                    nuevo.DayOfWeekEnabled = auxPlantilla.PerWeek;
                    if (null == auxPlantilla.Tokens)
                    {
                        nuevo.Tokens = new List<string>();
                        nuevo.Tokens.Add(nuevo.Name);
                    }
                    else
                    {
                        nuevo.Tokens = auxPlantilla.Tokens.Split(',').ToList();
                    }
                    if (nuevo is AttTemplateModel auxAtencion)
                    {
                        auxAtencion.StartTime = auxPlantilla.StartTime;
                        auxAtencion.Duration = auxPlantilla.Duration;
                        auxAtencion.Content = await TemplateContents(auxPlantilla.Id);
                    }
                    else if (nuevo is WorkTemplateModel auxTrabajo)
                    {
                        auxTrabajo.StartTime = auxPlantilla.StartTime;
                        auxTrabajo.Duration = auxPlantilla.Duration;
                        auxTrabajo.Content = await TemplateContents(auxPlantilla.Id);
                    }
                    else //Tiene que ser un descanso
                    {
                        RestTemplateModel auxDescanso = (RestTemplateModel)nuevo;
                    }
                }
                if (null != nuevo)
                {
                    content.Add(nuevo);
                }
            }
        }
        private async Task<List<WorkShiftContentModel>> TemplateContents(Guid parentId)
        {
            List<WorkShiftContentModel> salida = new List<WorkShiftContentModel>();
            using (DataStorage almacen = new DataStorage(mvarConfig))
            {
                List<WorkShiftContent> contenidos = await almacen.WorkShiftContents
                    .Where(x => x.Parent == parentId).OrderBy(x => x.Begin)
                    .ToListAsync();
                foreach(WorkShiftContent contenido in contenidos)
                {
                    WorkShiftContentModel nuevo;
                    if(null==contenido.TrainId)
                    {
                        //Depósito o atención a trenes
                        nuevo = new AttWorkShiftContentModel();
                        ((AttWorkShiftContentModel)nuevo).Foreign = contenido.Foreign;
                    }
                    else
                    {
                        //Tren
                        nuevo = new TrainWorkShiftContentModel();
                        ((TrainWorkShiftContentModel)nuevo).TrainId = contenido.TrainId;
                        ((TrainWorkShiftContentModel)nuevo).Discrectional = contenido.Discrectional;
                    }
                    nuevo.StartTime = contenido.Begin;
                    nuevo.EndTime = contenido.EndTime;
                    salida.Add(nuevo);
                }
            }
            return salida;
        }

    }
}
