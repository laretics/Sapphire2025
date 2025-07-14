using Sapphire2025Models.Expert;
using System.Net.Http.Json;

namespace Sapphire2025.Storage
{
    public class ExpertClient:HttpClientBase
    {
        public ExpertClient(HttpClient httpClient, IntStorageService intStorage) : base(httpClient, intStorage, "sapphireexpert") { }

        public async Task<string?> uploadXML(Stream xmlSourceCode, string fileName)
        {
            using MultipartFormDataContent contenido = new MultipartFormDataContent();
            StreamContent streamContent = new StreamContent(xmlSourceCode);
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/xml");
            contenido.Add(streamContent, "file", fileName);
            HttpResponseMessage respuesta = await sendPostRequest("uploadxml", contenido);
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
        public async Task<AgentsViewModel?> getAgentsView(string viewName)
        {
            AgentsViewRequestModel request = new AgentsViewRequestModel();
            request.ViewId = viewName;
            string json = System.Text.Json.JsonSerializer.Serialize(request);
            HttpResponseMessage respuesta = await sendPostRequest("getagentviewtable", json);
            if (respuesta.IsSuccessStatusCode)
                return await respuesta.Content.ReadFromJsonAsync<AgentsViewModel>();
            return null;
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

        public async Task<List<AgentAssignationsModel>> AsignationsByDateAndGraph(DateTime begin, int days, string? agentsTableId)
        {
            if(null!=agentsTableId)
            {
                WorkShiftRequestModel peticion = new WorkShiftRequestModel();
                peticion.Date = begin;
                peticion.Days = days;
                peticion.AgentsTableId = agentsTableId;
                string json = System.Text.Json.JsonSerializer.Serialize(peticion);
                HttpResponseMessage respuesta = await sendPostRequest("assignationsgraph", json);
                if (respuesta.IsSuccessStatusCode)
                {
                    List<AgentAssignationsModel>? salida = await respuesta.Content.ReadFromJsonAsync<List<AgentAssignationsModel>>();
                    if (null != salida) return salida;
                }
            }
            return new List<AgentAssignationsModel>();
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
        public async Task<WorkShiftTemplateCollectionModel?> WorkShiftTemplateCollectionItem(Guid id, DateTime date, bool onlyWork)
        {
            Sapphire2025Models.Expert.WorkShiftRequestModel peticion = new WorkShiftRequestModel();
            peticion.Id = id;
            peticion.Date = date; //Es para filtrar el día de la semana que es.
            peticion.onlyWork = onlyWork; //Sólo carga los turnos que sean de trabajo (para gráfico diario)
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
		/// <summary>
		/// Obtiene una lista de las posibles vistas de Agentes.
		/// Se usa en el menú lateral para seleccionar gráficos.
		/// </summary>
		/// <returns>Una lista de strings con los nombres</returns>
		public async Task<List<string>?> getAgentsViews()
		{
            string request = composeCommand("agentslistsnames");
            HttpResponseMessage respuesta = await sendGetRequest(request);
            return await
                respuesta.Content.ReadFromJsonAsync<List<string>>();
		}
	}
}
