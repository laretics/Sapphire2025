using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sapphire2025Models.Expert;
using Sapphire2025Models.Expert.WorkshiftTemplates;
using Sapphire2025Server.Expert;
using Sapphire2025Server.Models;
using Sapphire2025Server.Models.Turnos;
using System.Text.Json;
using System.Xml;

namespace Sapphire2025Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class SapphireExpertController : SapphireBaseController
    {

        public SapphireExpertController(IConfiguration configuration) : base(configuration) { }
        [HttpPost("uploadxml")]
        public async Task<string> UploadXML([FromForm] IFormFile file)
        {
            XmlDocument auxDocumento = new XmlDocument();
            try
            {
                using (Stream stream = file.OpenReadStream())
                {
                    auxDocumento.Load(stream);
                    UniversalXMLImporter importador = new UniversalXMLImporter(mvarConfig);
                    return await importador.ImportXML(auxDocumento);
                }
            }
            catch (XmlException ex)
            {
                return string.Format("Error de XML: {0}", ex.Message);
            }
        }
        
        /// <summary>
        /// Fuerza un borrado de las asignaciones que hay en la base de datos.
        /// </summary>
        /// <returns></returns>
        [HttpGet("assignationsclear")]
        public async Task<bool> AssignationsClear()
        {
            using (DataStorage almacen = new DataStorage(mvarConfig))
            {
                List<WorkshiftAssignation> origen = await almacen.WorkShiftAssignations.ToListAsync();
                almacen.RemoveRange(origen);
                await almacen.SaveChangesAsync();
                return true;
            }
        }

        /// <summary>
        /// Nueva vista de gráfico mensual.
        /// En este caso vamos a pasar toda la carga de filtrado por agentes al FrontEnd.
        /// </summary>
        /// <param name="rhs">Modelo que contiene la fecha de inicio, el número de días y la lista de Agentes que se quiere representar.</param>
        /// <returns>Lista de todas las asignaciones válidas en estas fechas (para todos los Agentes)</returns>
        [HttpPost("assignations")]
        public async Task<List<AssignationContentModel>> Assignations(WorkShiftRequestModel rhs)
        {
            List<AssignationContentModel> salida = new List<AssignationContentModel>();
            using (DataStorage almacen = new DataStorage(mvarConfig))
            {
                DateTime fechaFin = rhs.Date.AddDays(rhs.Days);
                List<WorkshiftAssignation> auxOrigen = await almacen.WorkShiftAssignations.
                    Where(x => x.Date >= rhs.Date && x.Date < fechaFin).
                    ToListAsync();
                foreach (WorkshiftAssignation elemento in auxOrigen)
                {
                    AssignationContentModel nuevo = new AssignationContentModel();
                    nuevo.AgentId = elemento.Agent;
                    nuevo.Date = elemento.Date;
                    nuevo.AssignationsChain = elemento.Assignation;
                    nuevo.Comment = elemento.Annotation;
                    nuevo.Definitive = elemento.Definitive;
                    nuevo.SwappingAgent = elemento.SwappingAgent;
                    nuevo.TD = elemento.IsTD;
                    salida.Add(nuevo);
                }
            }            
		    return salida;
        }

        /// <summary>
        /// Obtiene el template correspondiente a la fecha correspondiente
        /// </summary>
        /// <param name="id">Guid del plan de explotación.</param>
        /// <returns></returns>
        [HttpPost("getplan")]
        public async Task<WorkShiftTemplateCollectionModel?> GetPlan(WorkShiftRequestModel request)
        {
            WorkShiftProcessor procesador = new WorkShiftProcessor(mvarConfig);
            return await procesador.Plan(request.Id);
        }

        /// <summary>
        /// Obtiene el Guid del plan de explotación correspondiente para una fecha concreta
        /// o Guid.Empty si no hay definido ninguno para esa fecha.
        /// </summary>
        /// <param name="date">Fecha para la que se busca el template</param>
        /// <returns>Guid del plan de explotación o Guid.Empty si no lo hay</returns>
        [HttpPost("planheader")]
        public async Task<Guid> PlanHeader(WorkShiftRequestModel request)
        {
            WorkShiftProcessor procesador = new WorkShiftProcessor(mvarConfig);
            return await procesador.HeaderPlan(request.Date, 0);
        }

        /// <summary>
        /// Obtiene la lista completa de planes.
        /// Mirar si esto puede quedarse obsoleto.
        /// </summary>
        /// <returns>La tabla de planes.</returns>
        [HttpGet("plans")]
        public async Task<List<WorkShiftTemplateCollectionModel>> Plans()
        {
            WorkShiftProcessor procesador = new WorkShiftProcessor(mvarConfig);
            return await procesador.Plans();
        }

        /// <summary>
        /// Obtiene un objeto dinámico de tipo PlansYearSlice con las asignaciones de planes a lo largo
        /// de todo el tiempo que se requiere.
        /// </summary>
        /// <param name="request">Objeto que contiene la fecha de inicio y el número de días</param>
        /// <returns>Un PlansYearSlice con las asignaciones de planes y festivos</returns>
        [HttpPost("planstimeslice")]
        public async Task<PlansYearSlice> PlansTimeSlice(WorkShiftRequestModel request)
        {
            WorkShiftProcessor procesador = new WorkShiftProcessor(mvarConfig);
            return await procesador.PlansTimeSlice(request.Date, request.Days);
        }

        /// <summary>
        /// Devuelve la lista de tablas de Agentes que se pueden mostrar en un gráfico de turnos.
        /// Es para menús, donde el usuario puede escoger una lista concreta.
        /// </summary>
        /// <returns>Lista de string, con los nombres de cada lista</returns>
        [HttpGet("agentslistscatalog")]
        public async Task<List<string>> AgentsListsCatalog()
        {
            WorkShiftProcessor procesador = new WorkShiftProcessor(mvarConfig);
            return await procesador.AgentsListsCatalog();
        }
        
        /// <summary>
        /// Vista de Agentes para un control de gráfico.
        /// </summary>
        /// <param name="name">Nombre de la vista de Agentes</param>
        /// <returns>Lista de vista de Agentes</returns>
        [HttpPost("agentsview")]
        public async Task<AgentsViewContainer> AgentsView(AgentsViewRequestModel rhs)
        {
            WorkShiftProcessor procesador = new WorkShiftProcessor(mvarConfig);
            return await procesador.AgentsViewList(rhs.ViewId);
        }

        /// <summary>
        /// Devuelve los siguientes festivos a partir de la fecha indicada en la petición
        /// </summary>
        /// <param name="request">Petición</param>
        /// <returns>Conjunto de festivos</returns>
        [HttpPost("nextfestives")]
        public async Task<HashSet<DateTime>> NextFestives([FromBody] FestivesRequestModel request)
        {
            WorkShiftProcessor processor = new WorkShiftProcessor(mvarConfig);
            return await processor.NextFestives(request.Date);
        }

        [HttpPost("getfestive")]
        public async Task<bool> IsFestive([FromBody] FestivesRequestModel request)
        {
            WorkShiftProcessor processor = new WorkShiftProcessor(mvarConfig);
            return await processor.IsFestive(request.Date);
        }

        [HttpPost("setfestive")]
        public async Task<bool> SetFestive([FromBody] FestivesRequestModel request)
        {
            WorkShiftProcessor processor = new WorkShiftProcessor(mvarConfig);
            return await processor.SetFestive(request.Date, request.Value);
        }

        /// <summary>
        /// Elimina un plan de explotación y todos los elementos que dependen de él.
        /// </summary>
        /// <param name="id">Id del plan</param>
        /// <returns>True si todo fue bien</returns>
        [HttpPost("deleteplan")]
        public async Task<bool> DeletePlan([FromBody] Guid id)
        {
            try
            {
                using (DataStorage almacen = new DataStorage(mvarConfig))
                {
                    //Eliminamos los trenes y los depósitos
                    IEnumerable<WorkShiftContent> contenidos = await almacen.WorkShiftContents
                        .Where(x => x.ParentCollection == id)
                        .ToListAsync();
                    if (contenidos.Any())
                        almacen.WorkShiftContents.RemoveRange(contenidos);

                    //Eliminamos los turnos
                    List<WorkShiftTemplate> turnos = await almacen.WorkShiftTemplates
                        .Where(x => x.Parent == id)
                        .ToListAsync();
                    if (turnos.Any())
                        almacen.WorkShiftTemplates.RemoveRange(turnos);

                    WorkShiftTemplateCollection? coleccion = await almacen.WorkShiftTemplateCollections
                        .FirstOrDefaultAsync(x => x.Id == id);

                    if (null != coleccion)
                        almacen.WorkShiftTemplateCollections.Remove(coleccion);

                    await almacen.SaveChangesAsync();
                }
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        /// <summary>
        /// Sube a la base de datos un gráfico que ha importado de Excel.
        /// </summary>
        /// <param name="auxDocumento">El documento en formato JSon separado por comas.</param>
        /// <returns>True si la importación se ha realizado de forma satisfactoria</returns>
        [HttpPost("uploadexcelgraph")]
        public async Task<string> UploadDailyWorkShift([FromBody] XlsxAssignUpdateModel? request)
        {
            if (null == request) return "Los datos de entrada son nulos.";
            if (null == request.ExcelDump) return "La hoja de cálculo que se ha recibido tiene un valor nulo";            
            List<List<AssignationCell>>? asignaciones = JsonSerializer.Deserialize<List<List<AssignationCell>>>(request.ExcelDump);
            ExcelGraphImporter importador = new ExcelGraphImporter(mvarConfig);
            return await importador.ProcessExcel(asignaciones,request.Date,request.Days);
        }
    } 
}
