using Sapphire2025Models.Aeneas;
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
        public async Task<List<Sapphire2025Models.Expert.WorkShiftTemplateCollectionModel>?> workShiftTemplateCollections()
        {
            string request = composeCommand("workshifttemplates");
            HttpResponseMessage respuesta = await sendGetRequest(request);
            return await respuesta.Content.ReadFromJsonAsync<List<Sapphire2025Models.Expert.WorkShiftTemplateCollectionModel>>();
        }
    }
}
