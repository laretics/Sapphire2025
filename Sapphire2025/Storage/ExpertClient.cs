using Sapphire2025Models.Expert;
using Sapphire2025Models.Expert.WorkshiftTemplates;
using System.Net.Http.Json;

namespace Sapphire2025.Storage
{
    public class ExpertClient:HttpClientBase
    {
        public ExpertClient(HttpClient httpClient, IntStorageService intStorage) : base(httpClient, intStorage, "sapphireexpert") { }

        public async Task<string?> UploadXML(Stream xmlSourceCode, string fileName)
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
        
        /// <summary>
        /// Fuerza un borrado de las asignaciones que hay en la base de datos.
        /// </summary>
        /// <returns></returns>
        public async Task<bool> AssignationsClear()
        {
            string request = composeCommand("assignationsclear");
            HttpResponseMessage respuesta = await sendGetRequest(request);
            return await respuesta.Content.ReadFromJsonAsync<bool>();
        }

        public async Task<List<AssignationContentModel>?> Assignations(DateTime date, int dayCount=1)
        {
            WorkShiftRequestModel request = new WorkShiftRequestModel();
            request.Date = date;
            request.Days = dayCount;
            string json = System.Text.Json.JsonSerializer.Serialize(request);
            HttpResponseMessage response = await sendPostRequest("assignations", json);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<List<AssignationContentModel>>();

            return null;
        }

        /// <summary>
        /// Descarga un plan de explotación completo.
        /// </summary>
        /// <param name="id">Guid del plan de explotación que se pretende descargar</param>
        /// <returns>El plan de explotación o null, si no existe ninguno con ese Guid</returns>
        public async Task<WorkShiftTemplateCollectionModel?> GetPlan(Guid id, DateTime date, bool onlyWork)
        {
            Sapphire2025Models.Expert.WorkShiftRequestModel peticion = new WorkShiftRequestModel();
            peticion.Id = id;
            peticion.Date = date; //Es para filtrar el día de la semana que es.
            peticion.onlyWork = onlyWork; //Sólo carga los turnos que sean de trabajo (para gráfico diario)
            string json = System.Text.Json.JsonSerializer.Serialize(peticion);
            HttpResponseMessage respuesta = await sendPostRequest("getplan", json);
            if (respuesta.IsSuccessStatusCode)
                return await respuesta.Content.ReadFromJsonAsync<WorkShiftTemplateCollectionModel>();
            return null;
        }
        /// <summary>
        /// Busca un plan de explotación definido para esta fecha.
        /// </summary>
        /// <param name="date">Fecha de los turnos correspondientes al plan de explotación</param>
        /// <returns>Guid del plan de explotación/returns>
        public async Task<Guid> PlanHeader(DateTime date)
        {
            Sapphire2025Models.Expert.WorkShiftRequestModel peticion = new WorkShiftRequestModel();
            peticion.Date = date;
            string json = System.Text.Json.JsonSerializer.Serialize(peticion);
            HttpResponseMessage respuesta = await sendPostRequest("planheader", json);
            if (respuesta.IsSuccessStatusCode)
                return await respuesta.Content.ReadFromJsonAsync<Guid>();
            return Guid.Empty; //Esto es si no encuentra nada en la base de datos.
        }

        /// <summary>
        /// Obtiene la lista completa de planes.
        /// Mirar si esto puede quedarse obsoleto.
        /// </summary>
        /// <returns>La tabla de planes.</returns>
        public async Task<List<Sapphire2025Models.Expert.WorkShiftTemplateCollectionModel>?> Plans()
        {
            string request = composeCommand("plans");
            HttpResponseMessage respuesta = await sendGetRequest(request);
            return await respuesta.Content.ReadFromJsonAsync<List<Sapphire2025Models.Expert.WorkShiftTemplateCollectionModel>>();
        }

        /// <summary>
        /// Devuelve un objeto dinámico de tipo PlansYearSlice con las asignaciones de planes a lo largo
        /// de todo el tiempo que se requiere.
        /// También contiene las festividades laborales.
        /// </summary>
        /// <param name="date">Fecha de inicio</param>
        /// <param name="dayCount">Número de días a retornar</param>
        /// <returns></returns>
        public async Task<PlansYearSlice?> PlansTimeSlice(DateTime date, int dayCount=1)
        {
            WorkShiftRequestModel request = new WorkShiftRequestModel();
            request.Date = date;
            request.Days = dayCount;
            string json = System.Text.Json.JsonSerializer.Serialize(request);
            HttpResponseMessage respuesta = await sendPostRequest("planstimeslice", json);
            if (respuesta.IsSuccessStatusCode)
                return await respuesta.Content.ReadFromJsonAsync<PlansYearSlice>();

            return null;
        }

        /// <summary>
        /// Devuelve la lista de tablas de Agentes que se pueden mostrar en un gráfico de turnos.
        /// Es para menús, donde el usuario puede escoger una lista concreta.
        /// </summary>
        /// <returns>Lista de string, con los nombres de cada lista</returns>
        public async Task<List<string>?> AgentsListsCatalog()
        {
            string request = composeCommand("agentslistscatalog");
            HttpResponseMessage respuesta = await sendGetRequest(request);
            return await
                respuesta.Content.ReadFromJsonAsync<List<string>>();
        }

        /// <summary>
        /// Vista de Agentes para un control de gráfico.
        /// </summary>
        /// <param name="viewName">Nombre de la vista de Agentes</param>
        /// <returns>Lista de vista de Agentes</returns>
        public async Task<AgentsViewContainer?> AgentsView(string viewName)
        {
            AgentsViewRequestModel request = new AgentsViewRequestModel();
            request.ViewId = viewName;
            string json = System.Text.Json.JsonSerializer.Serialize(request);
            HttpResponseMessage respuesta = await sendPostRequest("agentsview", json);
            if (respuesta.IsSuccessStatusCode)
            {
                AgentsViewContainer? salida = await respuesta.Content.ReadFromJsonAsync<AgentsViewContainer>();
                return salida;
            }                         
            return null;
        }

        /// <summary>
        /// Obtiene un conjunto con los días festivos a partir de la fecha especificada.
        /// </summary>
        /// <param name="today">La fecha de inicio (para descartar festivos pasados)</param>
        /// <returns>El conjunto de los festivos</returns>
        public async Task<HashSet<DateTime>?> NextFestives(DateTime today)
        {
            FestivesRequestModel requestModel = new FestivesRequestModel();
            requestModel.Date = today;
            string json = System.Text.Json.JsonSerializer.Serialize(requestModel);
            HttpResponseMessage respuesta = await sendPostRequest("nextfestives", json);
            if (respuesta.IsSuccessStatusCode)
                return await respuesta.Content.ReadFromJsonAsync<HashSet<DateTime>>();

            return null;
        }

        public async Task<bool> IsFestive(DateTime day)
        {
            FestivesRequestModel requestModel = new FestivesRequestModel();
            requestModel.Date = day;
            string json = System.Text.Json.JsonSerializer.Serialize(requestModel);
            HttpResponseMessage respuesta = await sendPostRequest("getfestive", json);
            if (respuesta.IsSuccessStatusCode)
                return await respuesta.Content.ReadFromJsonAsync<bool>();

            return false;
        }

        /// <summary>
        /// Elimina un plan de explotación y todos los elementos que dependen de él.
        /// </summary>
        /// <param name="id">Id del plan</param>
        /// <returns>True si todo fue bien</returns>
        public async Task<bool> DeletePlan(Guid id)
        {
            string json = System.Text.Json.JsonSerializer.Serialize(id);
            HttpResponseMessage respuesta = await sendPostRequest("deleteplan", json);
            if (respuesta.IsSuccessStatusCode)
                return await respuesta.Content.ReadFromJsonAsync<bool>();
            return false;
        }

        /// <summary>
        /// Sube a la base de datos un gráfico que ha importado de Excel.
        /// </summary>
        /// <param name="auxDocument">Nombre del archivo.</param>
        /// <param name="date">Fecha de comienzo de importación.</param>
        /// <param name="days">Número de días.</param>
        /// <returns>True si la importación se ha realizado de forma satisfactoria</returns>
        public async Task<string?> UploadDailyWorkShift(string auxDocument, DateTime date, int days)
        {
            XlsxAssignUpdateModel data = new();
            data.Date = date;
            data.Days = days;
            data.ExcelDump = auxDocument;
            string json = System.Text.Json.JsonSerializer.Serialize(data);
            HttpResponseMessage respuesta = await sendPostRequest("uploadexcelgraph", json);
            if (respuesta.IsSuccessStatusCode)
                return await respuesta.Content.ReadAsStringAsync();
            return "Error desconocido en el cliente.";
        }
    }
}
