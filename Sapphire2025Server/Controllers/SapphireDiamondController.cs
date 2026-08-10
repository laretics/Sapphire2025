using System.IO.Compression;
using Diamond.Timed;
using Diamond.Topo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Sapphire2025Models.Diamond;
using Sapphire2025Server.Comunications;
using Sapphire2026.Data;
using Sapphire2026.Data.Diamond;
using Sapphire2026.Data.Models;
using Sapphire2026.Data.Models.Diamond;

namespace Sapphire2025Server.Controllers
{
	/// <summary>
	/// API de artefactos Diamond en Sapphire (topologías documentales versionadas).
	/// </summary>
	[ApiController]
	[Route("api/[controller]")]
	public class SapphireDiamondController : SapphireBaseController
	{
		public SapphireDiamondController(
			IConfiguration configuration,
			IHubContext<SignalRHub> hubContext)
			: base(configuration, hubContext)
		{
		}

		/// <summary>Lista metadatos de topologías (sin payload).</summary>
		[HttpGet("topos")]
		public async Task<IEnumerable<DiamondTopoHeaderModel>> ListTopos(
			[FromQuery] bool activeOnly = true)
		{
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				DiamondTopoStore store = new DiamondTopoStore(almacen);
				return await store.ListHeadersAsync(activeOnly);
			}
		}

		/// <summary>Metadatos de una topología por Id.</summary>
		[HttpGet("topo")]
		public async Task<ActionResult<DiamondTopoHeaderModel>> GetTopo([FromQuery] Guid id)
		{
			if (Guid.Empty.Equals(id))
			{
				return BadRequest("Id vacío.");
			}

			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				DiamondTopoStore store = new DiamondTopoStore(almacen);
				DiamondTopoHeaderModel? header = await store.GetHeaderAsync(id);
				if (header is null)
				{
					return NotFound();
				}

				return header;
			}
		}

		/// <summary>Metadatos por hash de contenido SHA-256 (hex).</summary>
		[HttpGet("topobyhash")]
		public async Task<ActionResult<DiamondTopoHeaderModel>> GetTopoByHash(
			[FromQuery] string contentHash)
		{
			if (string.IsNullOrWhiteSpace(contentHash))
			{
				return BadRequest("contentHash vacío.");
			}

			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				DiamondTopoStore store = new DiamondTopoStore(almacen);
				DiamondTopoHeaderModel? header = await store.GetHeaderByContentHashAsync(contentHash);
				if (header is null)
				{
					return NotFound();
				}

				return header;
			}
		}

		/// <summary>Descarga el documento (XML o xml-gz) de una topología.</summary>
		[HttpGet("topocontent")]
		public async Task<IActionResult> DownloadTopoContent([FromQuery] Guid id)
		{
			if (Guid.Empty.Equals(id))
			{
				return BadRequest("Id vacío.");
			}

			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				DiamondTopoStore store = new DiamondTopoStore(almacen);
				DiamondTopoDocument? doc = await store.GetDocumentAsync(id);
				if (doc is null)
				{
					return NotFound();
				}

				string fileName = string.IsNullOrWhiteSpace(doc.SourceFileName)
					? doc.Name + ".xml"
					: doc.SourceFileName;
				string contentType = string.Equals(doc.Format, DiamondTopoStore.FormatXmlGz, StringComparison.OrdinalIgnoreCase)
					? "application/gzip"
					: "application/xml";
				return File(doc.Payload, contentType, fileName);
			}
		}

		/// <summary>
		/// Sube un XML topográfico Diamond (multipart: file + notas opcionales).
		/// Valida con <see cref="TopoXmlSerializer"/> y deduplica por ContentHash.
		/// </summary>
		[HttpPost("uploadtopo")]
		public async Task<DiamondTopoUploadResult> UploadTopo(
			[FromForm] IFormFile? file,
			[FromForm] string? notes,
			[FromForm] DateTime? validFrom)
		{
			DiamondTopoUploadResult result = new DiamondTopoUploadResult();
			if (file is null || file.Length == 0)
			{
				result.Success = false;
				result.Message = "No se ha recibido ningún archivo.";
				return result;
			}

			if (file.Length > DiamondTopoStore.MaxPayloadBytes)
			{
				result.Success = false;
				result.Message = string.Format(
					"El archivo supera el tamaño máximo ({0} bytes).",
					DiamondTopoStore.MaxPayloadBytes);
				return result;
			}

			byte[] payload;
			using (MemoryStream ms = new MemoryStream())
			{
				await file.CopyToAsync(ms);
				payload = ms.ToArray();
			}

			string format = DiamondTopoStore.FormatXml;
			byte[] xmlBytes = payload;
			string fileName = file.FileName ?? "topo.xml";
			if (fileName.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
				|| fileName.EndsWith(".xml-gz", StringComparison.OrdinalIgnoreCase))
			{
				format = DiamondTopoStore.FormatXmlGz;
				try
				{
					xmlBytes = Gunzip(payload);
				}
				catch (Exception ex)
				{
					result.Success = false;
					result.Message = "No se pudo descomprimir el gzip: " + ex.Message;
					return result;
				}
			}

			TopoLayout layout;
			try
			{
				using (MemoryStream xmlStream = new MemoryStream(xmlBytes, writable: false))
				{
					layout = TopoXmlSerializer.Load(xmlStream);
				}
			}
			catch (Exception ex)
			{
				result.Success = false;
				result.Message = "XML topográfico no válido: " + ex.Message;
				return result;
			}

			string structuralHash = MeshBinarySerializer.ComputeTopoFingerprint(layout);
			string name = layout.Info.Name;
			if (string.IsNullOrWhiteSpace(name))
			{
				name = Path.GetFileNameWithoutExtension(fileName);
			}

			string author = layout.Info.Author ?? string.Empty;
			string layoutId = layout.Info.Id ?? string.Empty;

			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				DiamondTopoStore store = new DiamondTopoStore(almacen);
				return await store.UpsertValidatedAsync(
					payload,
					format,
					name,
					author,
					layoutId,
					structuralHash,
					layout.Stations.Count,
					layout.Axes.Count,
					fileName,
					notes ?? string.Empty,
					validFrom);
			}
		}

		/// <summary>Baja lógica (IsActive = false). Rechazada si hay planes activos.</summary>
		[HttpPost("deletetopo")]
		public async Task<DiamondTopoUploadResult> DeleteTopo([FromBody] Guid id)
		{
			if (Guid.Empty.Equals(id))
			{
				return new DiamondTopoUploadResult { Success = false, Message = "Id vacío." };
			}

			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				DiamondTopoStore store = new DiamondTopoStore(almacen);
				return await store.SetActiveAsync(id, false);
			}
		}

		/// <summary>Reactiva una topología previamente desactivada.</summary>
		[HttpPost("activatetopo")]
		public async Task<DiamondTopoUploadResult> ActivateTopo([FromBody] Guid id)
		{
			if (Guid.Empty.Equals(id))
			{
				return new DiamondTopoUploadResult { Success = false, Message = "Id vacío." };
			}

			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				DiamondTopoStore store = new DiamondTopoStore(almacen);
				return await store.SetActiveAsync(id, true);
			}
		}

		// ── Planes de explotación (scripts Diamond) ──────────────────────────

		[HttpGet("plans")]
		public async Task<IEnumerable<DiamondPlanHeaderModel>> ListPlans(
			[FromQuery] bool activeOnly = true,
			[FromQuery] Guid? topoId = null)
		{
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				DiamondPlanStore store = new DiamondPlanStore(almacen);
				return await store.ListHeadersAsync(activeOnly, topoId);
			}
		}

		[HttpGet("plan")]
		public async Task<ActionResult<DiamondPlanHeaderModel>> GetPlan([FromQuery] Guid id)
		{
			if (Guid.Empty.Equals(id))
			{
				return BadRequest("Id vacío.");
			}

			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				DiamondPlanStore store = new DiamondPlanStore(almacen);
				DiamondPlanHeaderModel? header = await store.GetAsync(id, includeScript: true);
				if (header is null)
				{
					return NotFound();
				}

				return header;
			}
		}

		[HttpPost("saveplan")]
		public async Task<DiamondPlanSaveResult> SavePlan([FromBody] DiamondPlanSaveRequest request)
		{
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				DiamondPlanStore store = new DiamondPlanStore(almacen);
				return await store.SaveAsync(request);
			}
		}

		/// <summary>Sube un .ddm / texto de script (multipart: file + topoId + notes).</summary>
		[HttpPost("uploadplan")]
		public async Task<DiamondPlanSaveResult> UploadPlan(
			[FromForm] IFormFile? file,
			[FromForm] Guid topoId,
			[FromForm] string? notes,
			[FromForm] DateTime? validFrom)
		{
			DiamondPlanSaveResult result = new DiamondPlanSaveResult();
			if (file is null || file.Length == 0)
			{
				result.Success = false;
				result.Message = "No se ha recibido ningún archivo de plan.";
				return result;
			}

			if (Guid.Empty.Equals(topoId))
			{
				result.Success = false;
				result.Message = "Debe indicar topoId (topología del almacén).";
				return result;
			}

			string script;
			using (StreamReader reader = new StreamReader(file.OpenReadStream()))
			{
				script = await reader.ReadToEndAsync();
			}

			DiamondPlanSaveRequest request = new DiamondPlanSaveRequest
			{
				TopoId = topoId,
				SourceScript = script,
				SourceFileName = file.FileName,
				Notes = notes,
				ValidFrom = validFrom
			};

			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				DiamondPlanStore store = new DiamondPlanStore(almacen);
				return await store.SaveAsync(request);
			}
		}

		[HttpPost("deleteplan")]
		public async Task<DiamondPlanSaveResult> DeletePlan([FromBody] Guid id)
		{
			if (Guid.Empty.Equals(id))
			{
				return new DiamondPlanSaveResult { Success = false, Message = "Id vacío." };
			}

			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				DiamondPlanStore store = new DiamondPlanStore(almacen);
				return await store.SetActiveAsync(id, false);
			}
		}

		[HttpPost("activateplan")]
		public async Task<DiamondPlanSaveResult> ActivatePlan([FromBody] Guid id)
		{
			if (Guid.Empty.Equals(id))
			{
				return new DiamondPlanSaveResult { Success = false, Message = "Id vacío." };
			}

			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				DiamondPlanStore store = new DiamondPlanStore(almacen);
				return await store.SetActiveAsync(id, true);
			}
		}

		// ── Planes publicados (compilados para Tourmaline) ─────────────────

		[HttpGet("publishedplans")]
		public async Task<IEnumerable<DiamondPublishedPlanHeaderModel>> ListPublishedPlans(
			[FromQuery] bool activeOnly = true)
		{
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				DiamondPublishedPlanStore store = new DiamondPublishedPlanStore(almacen);
				return await store.ListHeadersAsync(activeOnly);
			}
		}

		/// <summary>Plan activo vigente para la fecha (por defecto hoy UTC).</summary>
		[HttpGet("publishedcurrent")]
		public async Task<ActionResult<DiamondPublishedPlanHeaderModel>> GetPublishedCurrent(
			[FromQuery] DateTime? date = null)
		{
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				DiamondPublishedPlanStore store = new DiamondPublishedPlanStore(almacen);
				DiamondPublishedPlanHeaderModel? header = await store.GetCurrentAsync(date ?? DateTime.UtcNow);
				if (header is null)
				{
					return NotFound();
				}

				return header;
			}
		}

		[HttpGet("publishedplan")]
		public async Task<ActionResult<DiamondPublishedPlanHeaderModel>> GetPublishedPlan([FromQuery] Guid id)
		{
			if (Guid.Empty.Equals(id))
			{
				return BadRequest("Id vacío.");
			}

			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				DiamondPublishedPlanStore store = new DiamondPublishedPlanStore(almacen);
				DiamondPublishedPlanHeaderModel? header = await store.GetHeaderAsync(id);
				if (header is null)
				{
					return NotFound();
				}

				return header;
			}
		}

		[HttpGet("publishedcontent")]
		public async Task<IActionResult> DownloadPublishedContent([FromQuery] Guid id)
		{
			if (Guid.Empty.Equals(id))
			{
				return BadRequest("Id vacío.");
			}

			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				DiamondPublishedPlanStore store = new DiamondPublishedPlanStore(almacen);
				DiamondPublishedPlanDocument? doc = await store.GetDocumentAsync(id);
				if (doc is null)
				{
					return NotFound();
				}

				string fileName = doc.Name + ".dpub.json";
				return File(doc.Payload, "application/json", fileName);
			}
		}

		/// <summary>Compila script+topo y almacena publicación inmutable.</summary>
		[HttpPost("publishplan")]
		public async Task<DiamondPublishPlanResult> PublishPlan([FromBody] DiamondPublishPlanRequest request)
		{
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				DiamondPublishedPlanStore store = new DiamondPublishedPlanStore(almacen);
				return await store.PublishAsync(request);
			}
		}

		[HttpPost("unpublishplan")]
		public async Task<DiamondPublishPlanResult> UnpublishPlan([FromBody] Guid id)
		{
			if (Guid.Empty.Equals(id))
			{
				return new DiamondPublishPlanResult { Success = false, Message = "Id vacío." };
			}

			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				DiamondPublishedPlanStore store = new DiamondPublishedPlanStore(almacen);
				return await store.SetActiveAsync(id, false);
			}
		}

		[HttpPost("republishplan")]
		public async Task<DiamondPublishPlanResult> RepublishPlan([FromBody] Guid id)
		{
			if (Guid.Empty.Equals(id))
			{
				return new DiamondPublishPlanResult { Success = false, Message = "Id vacío." };
			}

			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				DiamondPublishedPlanStore store = new DiamondPublishedPlanStore(almacen);
				return await store.SetActiveAsync(id, true);
			}
		}

		/// <summary>Actualiza metadatos de un plan compilado (CRUD, sin recompilar).</summary>
		[HttpPost("updatepublishedplan")]
		public async Task<DiamondPublishPlanResult> UpdatePublishedPlan(
			[FromBody] DiamondPublishedPlanUpdateRequest request)
		{
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				DiamondPublishedPlanStore store = new DiamondPublishedPlanStore(almacen);
				return await store.UpdateAsync(request);
			}
		}

		/// <summary>Borra del histórico un plan compilado (debe estar fuera de producción).</summary>
		[HttpPost("deletepublishedplan")]
		public async Task<DiamondPublishPlanResult> DeletePublishedPlan([FromBody] Guid id)
		{
			if (Guid.Empty.Equals(id))
			{
				return new DiamondPublishPlanResult { Success = false, Message = "Id vacío." };
			}

			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				DiamondPublishedPlanStore store = new DiamondPublishedPlanStore(almacen);
				return await store.DeleteAsync(id);
			}
		}

		// ── Dispositivos externos (trenes, SIU, enclavamientos…) ───────────

		/// <summary>
		/// Planes en producción para una topología, vigentes o próximos desde <paramref name="fromDate"/>.
		/// </summary>
		[HttpGet("device/production-plans")]
		public async Task<ActionResult<IEnumerable<DiamondPublishedPlanHeaderModel>>> ListDeviceProductionPlans(
			[FromQuery] Guid topoId,
			[FromQuery] DateTime? fromDate = null)
		{
			if (Guid.Empty.Equals(topoId))
			{
				return BadRequest("topoId vacío.");
			}

			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				DiamondPublishedPlanStore store = new DiamondPublishedPlanStore(almacen);
				IReadOnlyList<DiamondPublishedPlanHeaderModel> list =
					await store.ListForTopoAsync(topoId, fromDate ?? DateTime.UtcNow, inProductionOnly: true);
				return Ok(list);
			}
		}

		/// <summary>
		/// Paquete dispositivo: metadatos de topo (hash para caché) + planes en producción
		/// a partir de la fecha. El contenido de la topo se obtiene con topocontent?id=.
		/// </summary>
		[HttpGet("device/topo-package")]
		public async Task<ActionResult<DiamondDeviceTopoPackageModel>> GetDeviceTopoPackage(
			[FromQuery] Guid topoId,
			[FromQuery] DateTime? fromDate = null)
		{
			if (Guid.Empty.Equals(topoId))
			{
				return BadRequest("topoId vacío.");
			}

			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				DiamondPublishedPlanStore store = new DiamondPublishedPlanStore(almacen);
				DiamondDeviceTopoPackageModel? pkg =
					await store.BuildDevicePackageAsync(topoId, fromDate ?? DateTime.UtcNow);
				if (pkg is null)
				{
					return NotFound();
				}

				return pkg;
			}
		}

		/// <summary>Registra una emisión oficial de documentación de circulación.</summary>
		[HttpPost("circulation/emission")]
		public async Task<CirculationEmissionRegisterResult> RegisterCirculationEmission(
			[FromBody] CirculationEmissionRegisterRequest? request)
		{
			CirculationEmissionRegisterResult result = new CirculationEmissionRegisterResult();
			if (request is null || string.IsNullOrWhiteSpace(request.SealCode))
			{
				result.Success = false;
				result.Message = "Petición incompleta (falta sello).";
				return result;
			}

			string userId = string.Empty;
			if (!Guid.Empty.Equals(request.SessionToken))
			{
				User? actor = await retrieveSessionUser(request.SessionToken);
				if (actor is not null)
				{
					userId = actor.Id ?? string.Empty;
				}
			}

			string host = clientHostPoint() ?? string.Empty;
			DiamondCirculationEmission entity = new DiamondCirculationEmission
			{
				Id = request.EmissionId == Guid.Empty ? Guid.NewGuid() : request.EmissionId,
				EmittedAtUtc = DateTime.UtcNow,
				UserId = userId,
				DocumentKind = Trunc(request.DocumentKind, 16),
				Channel = Trunc(request.Channel, 16),
				SealCode = Trunc(request.SealCode.Replace("SEL", string.Empty, StringComparison.OrdinalIgnoreCase).Trim(), 32),
				Payload = Trunc(request.Payload, 1024),
				PlanOrTrain = Trunc(request.PlanOrTrain, 200),
				EditionLabel = Trunc(request.EditionLabel, 200),
				DayLabel = Trunc(request.DayLabel, 120),
				SheetCount = request.SheetCount,
				CertThumbprint = Trunc(request.CertThumbprint, 64),
				PdfContentHash = Trunc(request.PdfContentHash, 64),
				PdfCmsSignatureBase64 = request.PdfCmsSignatureBase64 ?? string.Empty,
				QrText = Trunc(request.QrText, 512),
				HostPoint = Trunc(host, 255)
			};

			try
			{
				using (DataStorage almacen = new DataStorage(mvarConfig))
				{
					DiamondCirculationEmissionStore store = new DiamondCirculationEmissionStore(almacen);
					await store.AddAsync(entity);
				}

				result.Success = true;
				result.EmissionId = entity.Id;
				result.Message = "Emisión registrada.";
			}
			catch (Exception ex)
			{
				result.Success = false;
				result.Message = "Error al registrar emisión: " + ex.Message;
			}

			return result;
		}

		/// <summary>Verifica un sello SEL o texto QR en el registro de emisiones.</summary>
		[HttpPost("circulation/verify")]
		public async Task<CirculationSealVerifyResponse> VerifyCirculationSeal(
			[FromBody] CirculationSealVerifyRequest? request)
		{
			CirculationSealVerifyResponse response = new CirculationSealVerifyResponse();
			if (request is null || string.IsNullOrWhiteSpace(request.SealOrQr))
			{
				response.Ok = false;
				response.Message = "Indica un sello o el texto del QR.";
				return response;
			}

			string seal = request.SealOrQr.Trim();
			// Extraer SEL de QR ZAFSEL:v1:{seal}:{payload}
			if (seal.StartsWith("ZAFSEL:v1:", StringComparison.OrdinalIgnoreCase))
			{
				string rest = seal.Substring("ZAFSEL:v1:".Length);
				int colon = rest.IndexOf(':');
				seal = colon > 0 ? rest.Substring(0, colon) : rest;
			}

			if (seal.StartsWith("SEL", StringComparison.OrdinalIgnoreCase))
			{
				seal = seal.Substring(3).Trim();
			}

			response.SealCode = seal;
			try
			{
				using (DataStorage almacen = new DataStorage(mvarConfig))
				{
					DiamondCirculationEmissionStore store = new DiamondCirculationEmissionStore(almacen);
					DiamondCirculationEmission? found = await store.FindBySealAsync(seal);
					if (found is null)
					{
						response.Ok = false;
						response.FoundInRegistry = false;
						response.Message = "Sello no encontrado en el registro de emisiones.";
						return response;
					}

					response.Ok = true;
					response.FoundInRegistry = true;
					response.Message = "Sello registrado: "
						+ found.DocumentKind + " · " + found.Channel
						+ " · " + found.PlanOrTrain
						+ " · " + found.EmittedAtUtc.ToString("u");
					response.Emission = ToModel(found);
					return response;
				}
			}
			catch (Exception ex)
			{
				response.Ok = false;
				response.Message = "Error de verificación: " + ex.Message;
				return response;
			}
		}

		private static CirculationEmissionModel ToModel(DiamondCirculationEmission e)
		{
			return new CirculationEmissionModel
			{
				Id = e.Id,
				EmittedAtUtc = e.EmittedAtUtc,
				UserId = e.UserId,
				DocumentKind = e.DocumentKind,
				Channel = e.Channel,
				SealCode = e.SealCode,
				Payload = e.Payload,
				PlanOrTrain = e.PlanOrTrain,
				EditionLabel = e.EditionLabel,
				DayLabel = e.DayLabel,
				SheetCount = e.SheetCount,
				CertThumbprint = e.CertThumbprint,
				PdfContentHash = e.PdfContentHash,
				QrText = e.QrText,
				HostPoint = e.HostPoint
			};
		}

		private static string Trunc(string? s, int max)
		{
			if (string.IsNullOrEmpty(s))
			{
				return string.Empty;
			}

			return s.Length <= max ? s : s.Substring(0, max);
		}

		private static byte[] Gunzip(byte[] gzipped)
		{
			using (MemoryStream input = new MemoryStream(gzipped, writable: false))
			using (GZipStream gzip = new GZipStream(input, CompressionMode.Decompress))
			using (MemoryStream output = new MemoryStream())
			{
				gzip.CopyTo(output);
				return output.ToArray();
			}
		}
	}
}
