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

    }
}
