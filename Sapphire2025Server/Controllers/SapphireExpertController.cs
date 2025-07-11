using BlazorBootstrap;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sapphire2025Models;
using Sapphire2025Models.Aeneas;
using Sapphire2025Models.Authentication;
using Sapphire2025Models.Expert;
using Sapphire2025Server.Expert;
using Sapphire2025Server.Models;
using Sapphire2025Server.Models.Turnos;
using Sapphire2025Server.Telegram;
using System.Collections;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security.Policy;
using System.Text;
using System.Xml;
using Telegram.Bot.Types.Passport;

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
        /// Obtiene el template correspondiente a la fecha correspondiente
        /// </summary>
        /// <param name="id">Guid del plan de explotación.</param>
        /// <returns></returns>
        [HttpPost("workshifttemplatecollectionitem")]
        public async Task<WorkShiftTemplateCollectionModel?> WorkShiftTemplateItem(WorkShiftRequestModel request)
        {
            WorkShiftTemplateCollectionModel? salida = null;
            using (DataStorage almacen = new DataStorage(mvarConfig))
            {
                WorkShiftTemplateCollection? candidato = await almacen.WorkShiftTemplateCollections.
                    Where(x => x.Id == request.Id).FirstOrDefaultAsync();
                if (null != candidato)
                {
                    User? usuario = await almacen.Users.
                        Where(x => x.CF.Equals(candidato.Owner)).FirstOrDefaultAsync();
                    salida = new WorkShiftTemplateCollectionModel();
                    salida.Id = request.Id;
                    salida.Name = candidato.Name;
                    salida.Comment = candidato.Comment;
                    salida.Collective = candidato.Collective;
                    salida.Begin = candidato.Begin;
                    salida.Owner = null == usuario ? Guid.Empty : usuario.guid;
                }
            }
            if (null != salida)
                salida.Templates = await getWorkShiftTemplates(request.Id);

            return salida;
        }

        internal async Task<Dictionary<string, WorkShiftTemplateModel>> getWorkShiftTemplates(Guid templateCollectionId)
        {
            Dictionary<string, WorkShiftTemplateModel> salida = new Dictionary<string, WorkShiftTemplateModel>();
            using (DataStorage almacen = new DataStorage(mvarConfig))
            {
                List<WorkShiftTemplate> colTemplates = await almacen.WorkShiftTemplates.
                    Where(x => x.Parent == templateCollectionId).ToListAsync();
                foreach (WorkShiftTemplate auxPlantilla in colTemplates)
                {
                    if (auxPlantilla.Active)
                    {
                        if (auxPlantilla.Att)
                        {
                            //Turno de depósito
                            AttTemplateModel nuevo = new AttTemplateModel();
                            nuevo.Name = auxPlantilla.Name;
                            nuevo.comment = auxPlantilla.Comment;
                            nuevo.Color = auxPlantilla.Color;
                            nuevo.BgColor = auxPlantilla.BgColor;
                            nuevo.StripeColor = auxPlantilla.StripeColor;
                            nuevo.StartTime = auxPlantilla.StartTime;
                            nuevo.Duration = auxPlantilla.Duration;
                            salida.Add(nuevo.Name, nuevo);
                        }
                        else
                        {
                            //Turno de conducción
                            WorkTemplateModel nuevo = new WorkTemplateModel();
                            nuevo.Name = auxPlantilla.Name;
                            nuevo.comment = auxPlantilla.Comment;
                            nuevo.Color = auxPlantilla.Color;
                            nuevo.BgColor = auxPlantilla.BgColor;
                            nuevo.StripeColor = auxPlantilla.StripeColor;
                            nuevo.StartTime = auxPlantilla.StartTime;
                            nuevo.Duration = auxPlantilla.Duration;
                            nuevo.Content = await getContents(auxPlantilla.Id);
                            salida.Add(nuevo.Name, nuevo);
                        }
                    }
                    else
                    {
                        //Descanso o licencia
                        RestTemplateModel nuevo = new RestTemplateModel();
                        nuevo.Name = auxPlantilla.Name;
                        nuevo.comment = auxPlantilla.Comment;
                        nuevo.Color = auxPlantilla.Color;
                        nuevo.BgColor = auxPlantilla.BgColor;
                        nuevo.StripeColor = auxPlantilla.StripeColor;
                        salida.Add(nuevo.Name, nuevo);
                    }
                }
            }
            return salida;
        }

        /// <summary>
        /// Obtiene los att y trenes de un determinado turno de trabajo.
        /// </summary>
        /// <param name="parentId">Guid del turno</param>
        /// <returns></returns>
        internal async Task<List<WorkShiftContentModel>> getContents(Guid parentId)
        {
            List<WorkShiftContentModel> salida = new List<WorkShiftContentModel>();
            using (DataStorage almacen = new DataStorage(mvarConfig))
            {
                List<WorkShiftContent> contenidos = await almacen.WorkShiftContents.
                    Where(x => x.Parent == parentId).ToListAsync();
                foreach (WorkShiftContent contenido in contenidos)
                {
                    if (null == contenido.TrainId)
                    {
                        //Depósito o att
                        AttWorkShiftContentModel nuevo = new AttWorkShiftContentModel();
                        nuevo.StartTime = contenido.Begin;
                        nuevo.EndTime = contenido.EndTime;
                        nuevo.Foreign = contenido.Foreign;
                        salida.Add(nuevo);
                    }
                    else
                    {
                        //Tren
                        TrainWorkShiftContentModel nuevo = new TrainWorkShiftContentModel();
                        nuevo.StartTime = contenido.Begin;
                        nuevo.EndTime = contenido.EndTime;
                        nuevo.TrainId = contenido.TrainId;
                        nuevo.Discrectional = contenido.Discrectional;
                        salida.Add(nuevo);
                    }
                }
            }
            return salida;
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
            using (DataStorage almacen = new DataStorage(mvarConfig))
            {
                WorkShiftTemplateCollection? candidato = await almacen.WorkShiftTemplateCollections.
                    OrderByDescending(x => x.Begin).
                    Where(x => x.Begin <= request.Date && x.Collective == 0).FirstOrDefaultAsync();
                if (null != candidato) return candidato.Id;
            }
            return Guid.Empty;
        }

        [HttpGet("workshifttemplates")]
        public async Task<List<Sapphire2025Models.Expert.WorkShiftTemplateCollectionModel>> WorkShiftTemplates()
        {
            List<Sapphire2025Models.Expert.WorkShiftTemplateCollectionModel> salida = new List<Sapphire2025Models.Expert.WorkShiftTemplateCollectionModel>();
            using (DataStorage almacen = new DataStorage(mvarConfig))
            {
                foreach (WorkShiftTemplateCollection origen in await almacen.WorkShiftTemplateCollections.OrderByDescending(x => x.Begin).ToListAsync())
                {
                    Sapphire2025Models.Expert.WorkShiftTemplateCollectionModel destino = new Sapphire2025Models.Expert.WorkShiftTemplateCollectionModel();
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
