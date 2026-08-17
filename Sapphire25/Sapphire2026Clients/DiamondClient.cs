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

		// ── Planes de explotación ───────────────────────────────────────────

		public async Task<IReadOnlyList<DiamondPlanHeaderModel>> ListPlansAsync(
			bool activeOnly = true,
			Guid? topoId = null)
		{
			List<requestParam> args = new List<requestParam>
			{
				new requestParam("activeOnly", activeOnly ? "true" : "false")
			};
			if (topoId.HasValue && !Guid.Empty.Equals(topoId.Value))
			{
				args.Add(new requestParam("topoId", topoId.Value.ToString()));
			}

			string request = composeCommand("plans", args.ToArray());
			HttpResponseMessage response = await sendGetRequest(request);
			List<DiamondPlanHeaderModel>? list =
				await response.Content.ReadFromJsonAsync<List<DiamondPlanHeaderModel>>();
			if (list is null)
			{
				return Array.Empty<DiamondPlanHeaderModel>();
			}

			return list;
		}

		public async Task<DiamondPlanHeaderModel?> GetPlanAsync(Guid id)
		{
			string request = composeCommand("plan", new requestParam("id", id.ToString()));
			try
			{
				HttpResponseMessage response = await sendGetRequest(request);
				return await response.Content.ReadFromJsonAsync<DiamondPlanHeaderModel>();
			}
			catch (HttpRequestException)
			{
				return null;
			}
		}

		public async Task<DiamondPlanSaveResult?> SavePlanAsync(DiamondPlanSaveRequest body)
		{
			string json = System.Text.Json.JsonSerializer.Serialize(body);
			HttpResponseMessage response = await sendPostRequest("saveplan", json);
			return await response.Content.ReadFromJsonAsync<DiamondPlanSaveResult>();
		}

		public async Task<DiamondPlanSaveResult?> UploadPlanAsync(
			Stream content,
			string fileName,
			Guid topoId,
			string? notes = null,
			DateTime? validFrom = null)
		{
			using MultipartFormDataContent form = new MultipartFormDataContent();
			StreamContent fileContent = new StreamContent(content);
			fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
			form.Add(fileContent, "file", fileName);
			form.Add(new StringContent(topoId.ToString()), "topoId");
			if (!string.IsNullOrEmpty(notes))
			{
				form.Add(new StringContent(notes), "notes");
			}

			if (validFrom.HasValue)
			{
				form.Add(new StringContent(validFrom.Value.ToString("o")), "validFrom");
			}

			HttpResponseMessage response = await sendPostRequest("uploadplan", form);
			return await response.Content.ReadFromJsonAsync<DiamondPlanSaveResult>();
		}

		public async Task<DiamondPlanSaveResult?> UploadPlanAsync(
			byte[] content,
			string fileName,
			Guid topoId,
			string? notes = null,
			DateTime? validFrom = null)
		{
			using MemoryStream stream = new MemoryStream(content, writable: false);
			return await UploadPlanAsync(stream, fileName, topoId, notes, validFrom);
		}

		public async Task<DiamondPlanSaveResult?> DeletePlanAsync(Guid id)
		{
			string json = System.Text.Json.JsonSerializer.Serialize(id);
			HttpResponseMessage response = await sendPostRequest("deleteplan", json);
			return await response.Content.ReadFromJsonAsync<DiamondPlanSaveResult>();
		}

		public async Task<DiamondPlanSaveResult?> ActivatePlanAsync(Guid id)
		{
			string json = System.Text.Json.JsonSerializer.Serialize(id);
			HttpResponseMessage response = await sendPostRequest("activateplan", json);
			return await response.Content.ReadFromJsonAsync<DiamondPlanSaveResult>();
		}

		// ── Publicados ────────────────────────────────────────────────────

		public async Task<IReadOnlyList<DiamondPublishedPlanHeaderModel>> ListPublishedPlansAsync(
			bool activeOnly = true)
		{
			string request = composeCommand(
				"publishedplans",
				new requestParam("activeOnly", activeOnly ? "true" : "false"));
			HttpResponseMessage response = await sendGetRequest(request);
			List<DiamondPublishedPlanHeaderModel>? list =
				await response.Content.ReadFromJsonAsync<List<DiamondPublishedPlanHeaderModel>>();
			if (list is null)
			{
				return Array.Empty<DiamondPublishedPlanHeaderModel>();
			}

			return list;
		}

		public async Task<DiamondPublishedPlanHeaderModel?> GetPublishedCurrentAsync(DateTime? date = null)
		{
			string request = date.HasValue
				? composeCommand(
					"publishedcurrent",
					new requestParam("date", date.Value.Date.ToString("yyyy-MM-dd")))
				: composeCommand("publishedcurrent");
			try
			{
				HttpResponseMessage response = await sendGetRequest(request);
				return await response.Content.ReadFromJsonAsync<DiamondPublishedPlanHeaderModel>();
			}
			catch (HttpRequestException)
			{
				return null;
			}
		}

		public async Task<DiamondPublishedPlanHeaderModel?> GetPublishedPlanAsync(Guid id)
		{
			string request = composeCommand("publishedplan", new requestParam("id", id.ToString()));
			try
			{
				HttpResponseMessage response = await sendGetRequest(request);
				return await response.Content.ReadFromJsonAsync<DiamondPublishedPlanHeaderModel>();
			}
			catch (HttpRequestException)
			{
				return null;
			}
		}

		public async Task<byte[]?> DownloadPublishedContentAsync(Guid id)
		{
			string request = composeCommand("publishedcontent", new requestParam("id", id.ToString()));
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

		public async Task<DiamondPublishPlanResult?> PublishPlanAsync(DiamondPublishPlanRequest body)
		{
			string json = System.Text.Json.JsonSerializer.Serialize(body);
			HttpResponseMessage response = await sendPostRequest("publishplan", json);
			return await response.Content.ReadFromJsonAsync<DiamondPublishPlanResult>();
		}

		public async Task<DiamondPublishPlanResult?> UnpublishPlanAsync(Guid id)
		{
			string json = System.Text.Json.JsonSerializer.Serialize(id);
			HttpResponseMessage response = await sendPostRequest("unpublishplan", json);
			return await response.Content.ReadFromJsonAsync<DiamondPublishPlanResult>();
		}

		public async Task<DiamondPublishPlanResult?> RepublishPlanAsync(Guid id)
		{
			string json = System.Text.Json.JsonSerializer.Serialize(id);
			HttpResponseMessage response = await sendPostRequest("republishplan", json);
			return await response.Content.ReadFromJsonAsync<DiamondPublishPlanResult>();
		}

		public async Task<DiamondPublishPlanResult?> UpdatePublishedPlanAsync(
			DiamondPublishedPlanUpdateRequest body)
		{
			string json = System.Text.Json.JsonSerializer.Serialize(body);
			HttpResponseMessage response = await sendPostRequest("updatepublishedplan", json);
			return await response.Content.ReadFromJsonAsync<DiamondPublishPlanResult>();
		}

		public async Task<DiamondPublishPlanResult?> DeletePublishedPlanAsync(Guid id)
		{
			string json = System.Text.Json.JsonSerializer.Serialize(id);
			HttpResponseMessage response = await sendPostRequest("deletepublishedplan", json);
			return await response.Content.ReadFromJsonAsync<DiamondPublishPlanResult>();
		}

		/// <summary>Planes en producción para una topología (API dispositivos / UI).</summary>
		public async Task<IReadOnlyList<DiamondPublishedPlanHeaderModel>> ListDeviceProductionPlansAsync(
			Guid topoId,
			DateTime? fromDate = null)
		{
			string request = fromDate.HasValue
				? composeCommand(
					"device/production-plans",
					new requestParam("topoId", topoId.ToString()),
					new requestParam("fromDate", fromDate.Value.ToString("o")))
				: composeCommand(
					"device/production-plans",
					new requestParam("topoId", topoId.ToString()));
			HttpResponseMessage response = await sendGetRequest(request);
			List<DiamondPublishedPlanHeaderModel>? list =
				await response.Content.ReadFromJsonAsync<List<DiamondPublishedPlanHeaderModel>>();
			if (list is null)
			{
				return Array.Empty<DiamondPublishedPlanHeaderModel>();
			}

			return list;
		}

		// ── Limitaciones temporales ───────────────────────────────────────

		public async Task<IReadOnlyList<DiamondTopoAxisModel>> ListTopoAxesAsync(Guid topoId)
		{
			string request = composeCommand(
				"topoaxes",
				new requestParam("id", topoId.ToString()));
			try
			{
				HttpResponseMessage response = await sendGetRequest(request);
				List<DiamondTopoAxisModel>? list =
					await response.Content.ReadFromJsonAsync<List<DiamondTopoAxisModel>>();
				if (list is null)
				{
					return Array.Empty<DiamondTopoAxisModel>();
				}

				return list;
			}
			catch (HttpRequestException)
			{
				return Array.Empty<DiamondTopoAxisModel>();
			}
		}

		public async Task<IReadOnlyList<DiamondTemporaryLimitModel>> ListTemporaryLimitsAsync(
			Guid topoId,
			string? axisId = null)
		{
			List<requestParam> args = new List<requestParam>
			{
				new requestParam("topoId", topoId.ToString())
			};
			if (!string.IsNullOrWhiteSpace(axisId))
			{
				args.Add(new requestParam("axisId", axisId));
			}

			string request = composeCommand("templimits", args.ToArray());
			try
			{
				HttpResponseMessage response = await sendGetRequest(request);
				List<DiamondTemporaryLimitModel>? list =
					await response.Content.ReadFromJsonAsync<List<DiamondTemporaryLimitModel>>();
				if (list is null)
				{
					return Array.Empty<DiamondTemporaryLimitModel>();
				}

				return list;
			}
			catch (HttpRequestException)
			{
				return Array.Empty<DiamondTemporaryLimitModel>();
			}
		}

		public async Task<DiamondTemporaryLimitSaveResult?> SaveTemporaryLimitAsync(
			DiamondTemporaryLimitSaveRequest body)
		{
			string json = System.Text.Json.JsonSerializer.Serialize(body);
			try
			{
				HttpResponseMessage response = await sendPostRequest("savetemplimit", json);
				return await response.Content.ReadFromJsonAsync<DiamondTemporaryLimitSaveResult>();
			}
			catch (HttpRequestException)
			{
				return null;
			}
		}

		public async Task<DiamondTemporaryLimitSaveResult?> DeleteTemporaryLimitAsync(Guid id)
		{
			string json = System.Text.Json.JsonSerializer.Serialize(id);
			try
			{
				HttpResponseMessage response = await sendPostRequest("deletetemplimit", json);
				return await response.Content.ReadFromJsonAsync<DiamondTemporaryLimitSaveResult>();
			}
			catch (HttpRequestException)
			{
				return null;
			}
		}

		public async Task<DiamondConsignaGenerationStatus?> GetConsignaGenerationAsync()
		{
			string request = composeCommand("consignageneration");
			try
			{
				HttpResponseMessage response = await sendGetRequest(request);
				return await response.Content.ReadFromJsonAsync<DiamondConsignaGenerationStatus>();
			}
			catch (HttpRequestException)
			{
				return null;
			}
		}

		public async Task<DiamondConsignaGenerationCloseResult?> CloseConsignaGenerationAsync()
		{
			try
			{
				HttpResponseMessage response = await sendPostRequest("closeconsignageneration", "{}");
				return await response.Content.ReadFromJsonAsync<DiamondConsignaGenerationCloseResult>();
			}
			catch (HttpRequestException)
			{
				return null;
			}
		}

		public async Task<CirculationEmissionRegisterResult?> RegisterCirculationEmissionAsync(
			CirculationEmissionRegisterRequest body)
		{
			if (body is null)
			{
				return null;
			}

			body.SessionToken = await getCurrentToken();
			string json = System.Text.Json.JsonSerializer.Serialize(body);
			try
			{
				HttpResponseMessage response = await sendPostRequest("circulation/emission", json);
				return await response.Content.ReadFromJsonAsync<CirculationEmissionRegisterResult>();
			}
			catch (HttpRequestException)
			{
				return null;
			}
		}

		public async Task<CirculationSealVerifyResponse?> VerifyCirculationSealAsync(string sealOrQr)
		{
			CirculationSealVerifyRequest body = new CirculationSealVerifyRequest
			{
				SessionToken = await getCurrentToken(),
				SealOrQr = sealOrQr ?? string.Empty
			};
			string json = System.Text.Json.JsonSerializer.Serialize(body);
			try
			{
				HttpResponseMessage response = await sendPostRequest("circulation/verify", json);
				return await response.Content.ReadFromJsonAsync<CirculationSealVerifyResponse>();
			}
			catch (HttpRequestException)
			{
				return null;
			}
		}

		public async Task<CirculationEmissionDocumentResponse?> GetCirculationDocumentAsync(string seal)
		{
			string request = composeCommand(
				"circulation/document",
				new requestParam("seal", seal ?? string.Empty));
			try
			{
				HttpResponseMessage response = await sendGetRequest(request);
				CirculationEmissionDocumentResponse? doc =
					await response.Content.ReadFromJsonAsync<CirculationEmissionDocumentResponse>();
				if (doc is not null && !string.IsNullOrEmpty(doc.SvgArchive) && doc.SvgPages.Count == 0)
				{
					// El cliente WASM no referencia el empaquetador; el host desempaqueta en UI.
				}

				return doc;
			}
			catch (HttpRequestException)
			{
				return null;
			}
		}

		// ── Festivos ──────────────────────────────────────────────────────

		public async Task<DiamondFestiveYearModel?> ListFestivesAsync(int year)
		{
			string request = composeCommand(
				"festives",
				new requestParam("year", year.ToString()));
			try
			{
				HttpResponseMessage response = await sendGetRequest(request);
				return await response.Content.ReadFromJsonAsync<DiamondFestiveYearModel>();
			}
			catch (HttpRequestException)
			{
				return null;
			}
		}

		public async Task<bool?> IsFestiveAsync(DateTime day)
		{
			string request = composeCommand(
				"isfestive",
				new requestParam("date", day.ToString("yyyy-MM-dd")));
			try
			{
				HttpResponseMessage response = await sendGetRequest(request);
				return await response.Content.ReadFromJsonAsync<bool>();
			}
			catch (HttpRequestException)
			{
				return null;
			}
		}

		public async Task<DiamondFestiveSetResult?> SetFestiveAsync(DateTime day, bool festive)
		{
			DiamondFestiveSetRequest body = new DiamondFestiveSetRequest
			{
				Date = day.ToString("yyyy-MM-dd"),
				Festive = festive
			};
			string json = System.Text.Json.JsonSerializer.Serialize(body);
			try
			{
				HttpResponseMessage response = await sendPostRequest("setfestive", json);
				return await response.Content.ReadFromJsonAsync<DiamondFestiveSetResult>();
			}
			catch (HttpRequestException)
			{
				return null;
			}
		}

		public async Task<DiamondDeviceTopoPackageModel?> GetDeviceTopoPackageAsync(
			Guid topoId,
			DateTime? fromDate = null)
		{
			string request = fromDate.HasValue
				? composeCommand(
					"device/topo-package",
					new requestParam("topoId", topoId.ToString()),
					new requestParam("fromDate", fromDate.Value.ToString("o")))
				: composeCommand(
					"device/topo-package",
					new requestParam("topoId", topoId.ToString()));
			try
			{
				HttpResponseMessage response = await sendGetRequest(request);
				return await response.Content.ReadFromJsonAsync<DiamondDeviceTopoPackageModel>();
			}
			catch (HttpRequestException)
			{
				return null;
			}
		}
	}
}
