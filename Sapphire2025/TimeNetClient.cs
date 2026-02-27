using Sapphire2025.Storage;
using Sapphire2025Models.ScriptCompiling;
using System.Net.Http.Json;

namespace Sapphire2025
{
	public class TimeNetClient:HttpClientBase
	{
		public TimeNetClient(HttpClient http, IntStorageService intStorage) : base(http, intStorage, "shappiretimenet") { }
		public async Task<XMLCompileResult?> UploadXML(Stream xmlSourceCode, string fileName)
		{
			using MultipartFormDataContent contenido = new MultipartFormDataContent();
			StreamContent streamContent = new StreamContent(xmlSourceCode);
			streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/xml");
			contenido.Add(streamContent, "file", fileName);
			HttpResponseMessage respuesta = await sendPostRequest("uploadxml", contenido);
			if(respuesta.IsSuccessStatusCode)
				return await respuesta.Content.ReadFromJsonAsync<XMLCompileResult>();

			return null; //En caso de error de comunicaciones
		}
	}
}
