using System.Net.Http.Headers;
using System.Net.Http.Json;
using Sapphire2025Models.Diamond;
using Sapphire2026Clients;

namespace Sapphire2025.Storage
{
	/// <summary>
	/// Cliente HTTP del controlador SapphireDiamond (topologías documentales).
	/// </summary>
	public class DiamondClient : HttpClientBase
	{
		public DiamondClient(
			HttpClient httpClient,
			IntStorageService intStorage,
			SessionService session)
			: base(httpClient, intStorage, session, "SapphireDiamond")
		{
		}

		/// <summary>Lista cabeceras de topologías almacenadas.</summary>
		public async Task<IReadOnlyList<DiamondTopoHeaderModel>> ListToposAsync(bool activeOnly = true)
		{
			string request = composeCommand(
				"topos",
				new requestParam("activeOnly", activeOnly ? "true" : "false"));
			HttpResponseMessage response = await sendGetRequest(request);
			List<DiamondTopoHeaderModel>? list =
				await response.Content.ReadFromJsonAsync<List<DiamondTopoHeaderModel>>();
			if (list is null)
			{
				return Array.Empty<DiamondTopoHeaderModel>();
			}

			return list;
		}

		public async Task<DiamondTopoHeaderModel?> GetTopoAsync(Guid id)
		{
			string request = composeCommand(
				"topo",
				new requestParam("id", id.ToString()));
			try
			{
				HttpResponseMessage response = await sendGetRequest(request);
				return await response.Content.ReadFromJsonAsync<DiamondTopoHeaderModel>();
			}
			catch (HttpRequestException)
			{
				return null;
			}
		}

		public async Task<DiamondTopoHeaderModel?> GetTopoByHashAsync(string contentHash)
		{
			string request = composeCommand(
				"topobyhash",
				new requestParam("contentHash", contentHash));
			try
			{
				HttpResponseMessage response = await sendGetRequest(request);
				return await response.Content.ReadFromJsonAsync<DiamondTopoHeaderModel>();
			}
			catch (HttpRequestException)
			{
				return null;
			}
		}

		/// <summary>Descarga el payload crudo (XML o gzip).</summary>
		public async Task<byte[]?> DownloadTopoContentAsync(Guid id)
		{
			string request = composeCommand(
				"topocontent",
				new requestParam("id", id.ToString()));
			try
			{
				HttpResponseMessage response = await sendGetRequest(request);
				return await response.Content.ReadAsByteArrayAsync();
			}
			catch (HttpRequestException)
			{
				return null;
			}
		}

		/// <summary>Sube un fichero topográfico (XML o .xml.gz).</summary>
		public async Task<DiamondTopoUploadResult?> UploadTopoAsync(
			Stream content,
			string fileName,
			string? notes = null,
			DateTime? validFrom = null)
		{
			using MultipartFormDataContent form = new MultipartFormDataContent();
			StreamContent fileContent = new StreamContent(content);
			fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
			form.Add(fileContent, "file", fileName);
			if (!string.IsNullOrEmpty(notes))
			{
				form.Add(new StringContent(notes), "notes");
			}

			if (validFrom.HasValue)
			{
				form.Add(new StringContent(validFrom.Value.ToString("o")), "validFrom");
			}

			HttpResponseMessage response = await sendPostRequest("uploadtopo", form);
			return await response.Content.ReadFromJsonAsync<DiamondTopoUploadResult>();
		}

		public async Task<DiamondTopoUploadResult?> UploadTopoAsync(
			byte[] content,
			string fileName,
			string? notes = null,
			DateTime? validFrom = null)
		{
			using MemoryStream stream = new MemoryStream(content, writable: false);
			return await UploadTopoAsync(stream, fileName, notes, validFrom);
		}

		public async Task<DiamondTopoUploadResult?> DeleteTopoAsync(Guid id)
		{
			string json = System.Text.Json.JsonSerializer.Serialize(id);
			HttpResponseMessage response = await sendPostRequest("deletetopo", json);
			return await response.Content.ReadFromJsonAsync<DiamondTopoUploadResult>();
		}

		public async Task<DiamondTopoUploadResult?> ActivateTopoAsync(Guid id)
		{
			string json = System.Text.Json.JsonSerializer.Serialize(id);
			HttpResponseMessage response = await sendPostRequest("activatetopo", json);
			return await response.Content.ReadFromJsonAsync<DiamondTopoUploadResult>();
		}
	}
}
