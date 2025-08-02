using Sapphire2025Models.Expert;
using Sapphire2025Server.Models.Turnos;
using Sapphire2025Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.Json;
using Org.BouncyCastle.Crypto.Operators;
using Sapphire2025Models.Expert.WorkshiftTemplates;

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

        public async Task<WorkShiftTemplateCollectionModel?> Plan(Guid id, bool actives, bool inactives, DateTime fecha)
        {
            int diaSemanaExp = (int)fecha.DayOfWeek;
            byte diaSemana = (byte)(Math.Pow(2,diaSemanaExp));
            if (await IsFestive(fecha))
                diaSemana |= 128;
            return await Plan(id, actives, inactives, diaSemana);
        }
        /// <summary>
        /// Obtiene el plan de explotación para un día concreto de la semana.
        /// </summary>
        /// <param name="id">Id del plan de explotación</param>
        /// <param name="actives">Recupera sólo los turnos de trabajo</param>
        /// <param name="inactives">Recupera sólo los turnos de descanso</param>
        /// <param name="dayOfWeek">Días de la semana, en formato flag (1,2,4,8,16...)</param>
        /// <returns></returns>
        public async Task<WorkShiftTemplateCollectionModel?> Plan(Guid id, bool actives, bool inactives, byte dayOfWeek=255)
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
                        included = await Plan(auxId, actives, inactives, dayOfWeek);
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
                salida.Templates = await Templates(id, actives, inactives, dayOfWeek);
                if (null == salida.Templates)
                    salida.Templates = new Dictionary<string, WorkShiftTemplateModel>();
                if (null != included && null != included.Templates)
                {
                    ///Creo que con esto tengo la herencia, pero hay que comprobarlo.
                    foreach (WorkShiftTemplateModel template in included.Templates.Values)
                        salida.Templates.Add(template.Name, template);
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
        public async Task SetFestive(DateTime rhs, bool value)
        {
            bool currentValue = await IsFestive(rhs);
            if(value!=currentValue)
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
                        if(null!=elemento)
                            almacen.Remove(elemento);                        
                    }
                    await almacen.SaveChangesAsync();
                }
            }
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
        #endregion

        protected async Task<Dictionary<string,WorkShiftTemplateModel>> Templates(Guid parent, bool actives, bool inactives, byte dayOfWeek=255)
        {
            Dictionary<string,WorkShiftTemplateModel> salida = new Dictionary<string, WorkShiftTemplateModel>();
            using (DataStorage almacen = new DataStorage(mvarConfig))
            {
                List<WorkShiftTemplate> colTemplates = await almacen.WorkShiftTemplates
                    .Where(x => x.Parent == parent && ((!actives || x.Active) || (!inactives || !x.Active)))
                    .ToListAsync();
                foreach(WorkShiftTemplate auxPlantilla in colTemplates)
                {
                    WorkShiftTemplateModel? nuevo = null;
                    if(!auxPlantilla.Active)
                    {
                        nuevo = new RestTemplateModel();
                    }
                    else
                    {
                        if (isDayCompatible(dayOfWeek, auxPlantilla.PerWeek))
                        {
                            if (auxPlantilla.Att) //Depósito
                                nuevo = new AttTemplateModel();
                            else
                                nuevo = new WorkTemplateModel();
                        }
                    }
                    if(null!=nuevo)
                    {
                        nuevo.Name = auxPlantilla.Name;
                        nuevo.comment = auxPlantilla.Comment;
                        nuevo.Color = auxPlantilla.Color;
                        nuevo.BgColor = auxPlantilla.BgColor;
                        nuevo.StripeColor = auxPlantilla.StripeColor;
                        nuevo.CoorX = auxPlantilla.CoorX;
                        nuevo.CoorY = auxPlantilla.CoorY;
                        if(nuevo.GetType()==typeof(AttTemplateModel))
                        {
                            AttTemplateModel auxAtencion = (AttTemplateModel)nuevo;
                            auxAtencion.StartTime = auxPlantilla.StartTime;
                            auxAtencion.Duration = auxPlantilla.Duration;                            
                        }
                        else if (nuevo.GetType()==typeof(WorkTemplateModel))
                        {
                            WorkTemplateModel auxTrabajo = (WorkTemplateModel)nuevo;
                            auxTrabajo.StartTime = auxPlantilla.StartTime;
                            auxTrabajo.Duration = auxPlantilla.Duration;
                            auxTrabajo.Content = await TemplateContents(auxPlantilla.Id);
                        }
                        else //Tiene que ser un descanso
                        {
                            RestTemplateModel auxDescanso = (RestTemplateModel)nuevo;
                        }
                    }
                    if(null!=nuevo)
                    {
                        salida.Add(nuevo.Name, nuevo);
                    }
                }
            }
            return salida;
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

        private bool isDayCompatible(byte dayOfWeek, byte templateDayId)
        {
            //TODO: Mirar más adelante los días festivos.
            return 0 != (dayOfWeek & templateDayId);
        }

    }
}
