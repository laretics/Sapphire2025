using System.Net.Http.Json;
using System.Text;
using TimeNet2026.Storage;
using TimeNet2026.ScriptCompiling;
using TimeNet2026.Models;
using MessagePack;
using Sapphire2026Clients;

namespace Sapphire2025.Storage
{
	public class TimeNetClient:HttpClientBase
	{
		public TimeNetClient(HttpClient http, IntStorageService intStorage, SessionService session) : base(http, intStorage,session, "SapphireTimeNet") 
		{}
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
		public async Task<CompileResult?> DeleteTopoStorage(Guid id)
		{
			string jsonData = System.Text.Json.JsonSerializer.Serialize(id);
			HttpResponseMessage respuesta = await sendPostRequest("deletetopostorage", jsonData);
			if (respuesta.IsSuccessStatusCode)
				return await respuesta.Content.ReadFromJsonAsync<CompileResult>();

			return null;
		}

		public async Task<TimeNetDataExportDto?> DownloadJsonPackageAsync(Guid token)
		{
			var response = await sendPostRequest("tnjsoncontent", System.Text.Json.JsonSerializer.Serialize(token));
			if (response.IsSuccessStatusCode)
				return await response.Content.ReadFromJsonAsync<TimeNetDataExportDto>();
			return null;
		}
		public async Task<TimeNetDataExportDto?> DownloadBinaryPackageAsync(Guid token)
		{
			var response = await sendPostRequest("tnbincontent", System.Text.Json.JsonSerializer.Serialize(token));
			if (response.IsSuccessStatusCode)
			{
				var buffer = await response.Content.ReadAsByteArrayAsync();
				return MessagePackSerializer.Deserialize<TimeNetDataExportDto>(buffer);
			}
			return null;
		}

	}
}
