using System.Net.Http.Json;
using System.Text;
using TimeNet2026.Storage;
using TimeNet2026.ScriptCompiling;
using TimeNet2026.Models;

namespace Sapphire2025.Storage
{
	public class TimeNetClient:HttpClientBase
	{
		//Almacén local TimeNet para poder "jugar" con la estructura en modo local sin sobrecargar las comunicaciones.
		public OnyxStorage LocalStorage { get; set; }
		public TimeNetClient(HttpClient http, IntStorageService intStorage) : base(http, intStorage, "SapphireTimeNet") 
		{
			LocalStorage = new OnyxStorage();
		}
		public async Task<CompileResult?> UploadXML(string xmlSourceCode, string fileName)
		{
			using MultipartFormDataContent contenido = new MultipartFormDataContent();
			Stream corriente = new MemoryStream(Encoding.UTF8.GetBytes(xmlSourceCode));
			StreamContent streamContent = new StreamContent(corriente);
			streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/xml");
			contenido.Add(streamContent, "file", fileName);
			HttpResponseMessage respuesta = await sendPostRequest("uploadxml", contenido);
			if(respuesta.IsSuccessStatusCode)
				return await respuesta.Content.ReadFromJsonAsync<CompileResult>();

			return null; //En caso de error de comunicaciones
		}
		/// <summary>
		/// Carga en la base de datos local (LocalStorage) la topología y los rautas.
		/// </summary>
		public async Task<IEnumerable<TopoStorageHeaderModel>?> GetTopoStorages()
		{
			string request = composeCommand("topostorages");
			HttpResponseMessage respuesta = await sendGetRequest(request);
			return await respuesta.Content.ReadFromJsonAsync<IEnumerable<TopoStorageHeaderModel>>();
		}
		public async Task<CompileResult> DeleteTopoStorage(Guid id)
		{
			return new CompileResult();
		}
	}
}
