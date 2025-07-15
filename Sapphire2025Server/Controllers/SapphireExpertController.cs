using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sapphire2025Models.Expert;
using Sapphire2025Server.Expert;
using Sapphire2025Server.Models;
using Sapphire2025Server.Models.Turnos;
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
        /// Obtiene todas las asignaciones laborales en el gráfico para una fecha concreta.
        /// </summary>
        /// <param name="rhs">Fecha de asignación</param>
        /// <returns>Lista de asignaciones en gráfico</returns>
        [HttpPost("assignationsbydate")]
        public async Task<List<WorkShiftAssignationModel>> getAssignations(WorkShiftRequestModel rhs)
        {
            List<WorkShiftAssignationModel> salida = new List<WorkShiftAssignationModel>();
            using (DataStorage almacen = new DataStorage(mvarConfig))
            {
                List<WorkshiftAssignation> origen = await almacen.WorkShiftAssignations.
                    Where(x => x.Date.Equals(rhs.Date)).ToListAsync();
                foreach (WorkshiftAssignation elemento in origen)
                {
                    if (null != elemento.Assignation && null != elemento.Definitive)
                    {
                        WorkShiftAssignationModel nuevo = new WorkShiftAssignationModel();
                        nuevo.Agent = elemento.Agent;
                        nuevo.Assignation = elemento.Assignation;
                        nuevo.Date = rhs.Date;
                        nuevo.Definitive = elemento.Definitive;
                        nuevo.IsTD = elemento.IsTD;
                        nuevo.SwappingAgent = elemento.SwappingAgent;
                        salida.Add(nuevo);
                    }
                }
            }
            return salida;
        }

        
        /// <summary>
        /// Nueva vista de gráfico mensual.
        /// </summary>
        /// <param name="rhs">Modelo que contiene la fecha de inicio, el número de días y la lista de Agentes que se quiere representar.</param>
        /// <returns>Una lista de AgentsAsignationsModel con los agentes y sus asignaciones</returns>
        [HttpPost("assignationsgraph")]
        public async Task<List<AgentAssignationsModel>> getGraph(WorkShiftRequestModel rhs)
        {
            if (null == rhs.AgentsTableId) return new List<AgentAssignationsModel>();
            AgentsListCompiler compiladorAgentes = new AgentsListCompiler(mvarConfig);            
            //Relleno la lista con asignaciones vacías pero con el encabezado correspondiente a cada Agente.            
            List<AgentAssignationsModel> salida = await compiladorAgentes.GetAssignationsContent(rhs.AgentsTableId,rhs.Date,rhs.Days);
		    return salida;
        }

        /// <summary>
        /// Obtiene el template correspondiente a la fecha correspondiente
        /// </summary>
        /// <param name="id">Guid del plan de explotación.</param>
        /// <returns></returns>
        [HttpPost("workshifttemplatecollectionitem")]
        public async Task<WorkShiftTemplateCollectionModel?> WorkShiftTemplateItem(WorkShiftRequestModel request)
        {
            WorkShiftProcessor procesador = new WorkShiftProcessor(mvarConfig);
            return await procesador.Plan(request.Id, request.onlyWork, true, request.Date);
        }

        /// <summary>
        /// Obtiene el Guid del plan de explotación correspondiente para una fecha concreta
        /// o Guid.Empty si no hay definido ninguno para esa fecha.
        /// </summary>
        /// <param name="date">Fecha para la que se busca el template</param>
        /// <returns>Guid del plan de explotación o Guid.Empty si no lo hay</returns>
        [HttpPost("workshifttemplateheader")]
        public async Task<Guid> WorkShiftTemplateHeader(WorkShiftRequestModel request)
        {
            WorkShiftProcessor procesador = new WorkShiftProcessor(mvarConfig);
            return await procesador.HeaderPlan(request.Date, 0);
        }

        [HttpGet("workshifttemplates")]
        public async Task<List<Sapphire2025Models.Expert.WorkShiftTemplateCollectionModel>> WorkShiftTemplates()
        {
            WorkShiftProcessor procesador = new WorkShiftProcessor(mvarConfig);
            return await procesador.Plans();
        }
        [HttpGet("agentslistsnames")]
        public async Task<List<string>> AgentsListsNames()
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

        [HttpPost("deleteworkshifttemplatecollection")]
        public async Task<bool> DeleteWorkShiftTemplateCollection([FromBody] Guid id)
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
        public async Task<string> uploadDailyWorkShift([FromBody] List<List<AssignationCell>>? filas)
        {
            ExcelGraphImporter importador = new ExcelGraphImporter();
            return await importador.ProcessExcel(filas, mvarConfig);
        }


    } 
}
