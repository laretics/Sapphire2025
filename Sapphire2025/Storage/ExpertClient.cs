using Sapphire2025Models.Aeneas;
using Sapphire2025Models.Expert;
using System.Net.Http.Json;

namespace Sapphire2025.Storage
{
    public class ExpertClient:HttpClientBase
    {
        public ExpertClient(HttpClient httpClient, IntStorageService intStorage) : base(httpClient, intStorage, "sapphireexpert") { }

        public async Task<string?> uploadXMLWorkShiftTemplate(Stream xmlSourceCode, string fileName)
        {
            using MultipartFormDataContent contenido = new MultipartFormDataContent();
            StreamContent streamContent = new StreamContent(xmlSourceCode);
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/xml");
            contenido.Add(streamContent, "file", fileName);
            HttpResponseMessage respuesta = await sendPostRequest("uploadxmlworkshift", contenido);
            if(respuesta.IsSuccessStatusCode)
                return await respuesta.Content.ReadAsStringAsync();
            return "Unknown error";
        }
        public async Task<string?> uploadDailyWorkShift(string auxDocument)
        {
            HttpResponseMessage respuesta = await sendPostRequest("uploadexcelgraph", auxDocument);
            if (respuesta.IsSuccessStatusCode)
                return await respuesta.Content.ReadAsStringAsync();
            return "Error desconocido en el cliente.";
        }
        public async Task<bool> deleteWorkShiftTemplateCollection(Guid id)
        {
            string json = System.Text.Json.JsonSerializer.Serialize(id);
            HttpResponseMessage respuesta = await sendPostRequest("deleteworkshifttemplatecollection", json);
            if (respuesta.IsSuccessStatusCode)
                return await respuesta.Content.ReadFromJsonAsync<bool>();
            return false;            
        }

        /// <summary>
        /// Devuelve las asignaciones de turnos realizadas para la fecha ordenadas por CF.
        /// </summary>
        /// <param name="date">Fecha de asignación</param>
        /// <returns>Lista de las asignaciones</returns>
        public async Task<List<WorkShiftAssignationModel>> AssignationsByDate(DateTime rhs)
        {
            Sapphire2025Models.Expert.WorkShiftRequestModel peticion = new WorkShiftRequestModel();
            peticion.Date = rhs;
            string json = System.Text.Json.JsonSerializer.Serialize(peticion);
            HttpResponseMessage respuesta = await sendPostRequest("assignationsbydate",json);
            if (respuesta.IsSuccessStatusCode)
            {
                List<WorkShiftAssignationModel>? salida = await respuesta.Content.ReadFromJsonAsync<List<WorkShiftAssignationModel>>();
                if (null != salida) return salida;
            }
            return new List<WorkShiftAssignationModel>();            
        }

        /// <summary>
        /// Busca un plan de explotación definido para esta fecha.
        /// </summary>
        /// <param name="date">Fecha de los turnos correspondientes al plan de explotación</param>
        /// <returns>Guid del plan de explotación/returns>
        public async Task<Guid> WorkShiftTemplateHeader(DateTime date)
        {
            Sapphire2025Models.Expert.WorkShiftRequestModel peticion = new WorkShiftRequestModel();
            peticion.Date = date;
            string json = System.Text.Json.JsonSerializer.Serialize(peticion);
            HttpResponseMessage respuesta = await sendPostRequest("workshifttemplateheader", json);
            if (respuesta.IsSuccessStatusCode)
                return await respuesta.Content.ReadFromJsonAsync<Guid>();
            return Guid.Empty; //Esto es si no encuentra nada en la base de datos.
        }
        /// <summary>
        /// Descarga un plan de explotación completo.
        /// </summary>
        /// <param name="id">Guid del plan de explotación que se pretende descargar</param>
        /// <returns>El plan de explotación o null, si no existe ninguno con ese Guid</returns>
        public async Task<WorkShiftTemplateCollectionModel?> WorkShiftTemplateCollectionItem(Guid id)
        {
            Sapphire2025Models.Expert.WorkShiftRequestModel peticion = new WorkShiftRequestModel();
            peticion.Id = id;
            string json = System.Text.Json.JsonSerializer.Serialize(peticion);
            HttpResponseMessage respuesta = await sendPostRequest("workshifttemplatecollectionitem", json);
            if (respuesta.IsSuccessStatusCode)
                return await respuesta.Content.ReadFromJsonAsync<WorkShiftTemplateCollectionModel>();
            return null;
        }
        public async Task<List<Sapphire2025Models.Expert.WorkShiftTemplateCollectionModel>?> workShiftTemplateCollections()
        {
            string request = composeCommand("workshifttemplates");
            HttpResponseMessage respuesta = await sendGetRequest(request);
            return await respuesta.Content.ReadFromJsonAsync<List<Sapphire2025Models.Expert.WorkShiftTemplateCollectionModel>>();
        }

    }
}
