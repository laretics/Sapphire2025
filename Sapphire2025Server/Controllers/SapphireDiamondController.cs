using System.IO.Compression;
using Diamond.Timed;
using Diamond.Topo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Sapphire2025Models;
using Sapphire2025Models.Diamond;
using Sapphire2025Server.Comunications;
using Sapphire2025Server.Storage;
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
				SealCode = Trunc(CirculationSealText.Normalize(request.SealCode), 32),
				Payload = Trunc(request.Payload, 1024),
				PlanOrTrain = Trunc(request.PlanOrTrain, 200),
				EditionLabel = Trunc(request.EditionLabel, 200),
				DayLabel = Trunc(request.DayLabel, 120),
				SheetCount = request.SheetCount,
				CertThumbprint = Trunc(request.CertThumbprint, 64),
				PdfContentHash = Trunc(request.PdfContentHash, 64),
				PdfCmsSignatureBase64 = request.PdfCmsSignatureBase64 ?? string.Empty,
				QrText = Trunc(request.QrText, 512),
				SvgArchive = request.SvgArchive ?? string.Empty,
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

			string seal = CirculationSealText.Normalize(request.SealOrQr);
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
					response.HasArchive = !string.IsNullOrWhiteSpace(found.SvgArchive);
					response.CryptographicMatch = true;
					response.Message = "Documento auténtico y reconocido. "
						+ KindLabel(found.DocumentKind)
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

		[HttpGet("circulation/document")]
		public async Task<CirculationEmissionDocumentResponse> GetCirculationDocument(
			[FromQuery] string? seal)
		{
			CirculationEmissionDocumentResponse response = new CirculationEmissionDocumentResponse();
			string normalized = CirculationSealText.Normalize(seal);
			if (string.IsNullOrWhiteSpace(normalized))
			{
				response.Ok = false;
				response.Message = "Indica el sello.";
				return response;
			}

			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				DiamondCirculationEmissionStore store = new DiamondCirculationEmissionStore(almacen);
				DiamondCirculationEmission? found = await store.FindBySealAsync(normalized);
				if (found is null)
				{
					response.Ok = false;
					response.Message = "Sello no encontrado.";
					return response;
				}

				if (string.IsNullOrWhiteSpace(found.SvgArchive))
				{
					response.Ok = false;
					response.Emission = ToModel(found);
					response.Message = "Emisión reconocida, pero no hay copia recuperable del documento.";
					return response;
				}

				response.Ok = true;
				response.Emission = ToModel(found);
				response.Message = "Documento recuperado.";
				response.SvgArchive = found.SvgArchive;
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
				HostPoint = e.HostPoint,
				HasArchive = !string.IsNullOrWhiteSpace(e.SvgArchive)
			};
		}

		private static string KindLabel(string kind)
		{
			if (string.Equals(kind, "libro", StringComparison.OrdinalIgnoreCase))
			{
				return "Libro itinerario";
			}

			if (string.Equals(kind, "ficha", StringComparison.OrdinalIgnoreCase))
			{
				return "Hoja de marcha";
			}

			if (string.Equals(kind, "consigna-b", StringComparison.OrdinalIgnoreCase))
			{
				return "Consigna serie B";
			}

			return kind ?? string.Empty;
		}

		// ── Limitaciones temporales de velocidad ───────────────────────────

		/// <summary>Ejes de una topología (id, PK, vmax) para el editor de limitaciones.</summary>
		[HttpGet("topoaxes")]
		public async Task<ActionResult<IReadOnlyList<DiamondTopoAxisModel>>> ListTopoAxes([FromQuery] Guid id)
		{
			if (Guid.Empty.Equals(id))
			{
				return BadRequest("Id vacío.");
			}

			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				DiamondTopoStore topoStore = new DiamondTopoStore(almacen);
				DiamondTopoDocument? doc = await topoStore.GetDocumentAsync(id);
				if (doc is null)
				{
					return NotFound();
				}

				try
				{
					byte[] xmlBytes = string.Equals(doc.Format, DiamondTopoStore.FormatXmlGz, StringComparison.OrdinalIgnoreCase)
						? Gunzip(doc.Payload)
						: doc.Payload;
					using (MemoryStream stream = new MemoryStream(xmlBytes, writable: false))
					{
						TopoLayout layout = TopoXmlSerializer.Load(stream);
						List<DiamondTopoAxisModel> axes = new List<DiamondTopoAxisModel>(layout.Axes.Count);
						int i = 0;
						while (i < layout.Axes.Count)
						{
							Axis axis = layout.Axes[i];
							DiamondTopoAxisModel item = new DiamondTopoAxisModel();
							item.Id = axis.Id;
							item.Name = string.IsNullOrWhiteSpace(axis.Name) ? axis.Id : axis.Name;
							item.Pk0 = axis.PK;
							item.Pkf = axis.PKEnd;
							item.Vmax = axis.Vmax;
							item.DefaultTrackCount = axis.DefaultTrackCount;
							IReadOnlyList<SpeedLimitSpan> fixedSpans = axis.FixedLimits.EnumerateStored();
							int s = 0;
							while (s < fixedSpans.Count)
							{
								SpeedLimitSpan span = fixedSpans[s];
								DiamondSpeedSpanModel stored = new DiamondSpeedSpanModel();
								stored.Pk0 = span.PK;
								stored.Pkf = span.PKEnd;
								stored.Speed = span.Speed;
								item.FixedLimits.Add(stored);
								s++;
							}

							axes.Add(item);
							i++;
						}

						return axes;
					}
				}
				catch (Exception ex)
				{
					return BadRequest("No se pudo leer la topología: " + ex.Message);
				}
			}
		}

		[HttpGet("templimits")]
		public async Task<IReadOnlyList<DiamondTemporaryLimitModel>> ListTemporaryLimits(
			[FromQuery] Guid topoId,
			[FromQuery] string? axisId = null)
		{
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				DiamondTemporaryLimitStore store = new DiamondTemporaryLimitStore(almacen);
				return await store.ListAsync(topoId, axisId);
			}
		}

		[HttpPost("savetemplimit")]
		public async Task<DiamondTemporaryLimitSaveResult> SaveTemporaryLimit(
			[FromBody] DiamondTemporaryLimitSaveRequest request)
		{
			if (request is null)
			{
				return new DiamondTemporaryLimitSaveResult
				{
					Success = false,
					Message = "Petición vacía."
				};
			}

			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				DiamondTemporaryLimitStore store = new DiamondTemporaryLimitStore(almacen);
				return await store.SaveAsync(request);
			}
		}

		[HttpPost("deletetemplimit")]
		public async Task<DiamondTemporaryLimitSaveResult> DeleteTemporaryLimit([FromBody] Guid id)
		{
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				DiamondTemporaryLimitStore store = new DiamondTemporaryLimitStore(almacen);
				return await store.DeleteAsync(id);
			}
		}

		[HttpGet("consignageneration")]
		public async Task<DiamondConsignaGenerationStatus> GetConsignaGeneration()
		{
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				DiamondConsignaGenerationStore store = new DiamondConsignaGenerationStore(almacen);
				return await store.GetStatusAsync();
			}
		}

		[HttpPost("closeconsignageneration")]
		public async Task<DiamondConsignaGenerationCloseResult> CloseConsignaGeneration()
		{
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				DiamondConsignaGenerationStore store = new DiamondConsignaGenerationStore(almacen);
				return await store.CloseAsync();
			}
		}

		// ── Festivos (tabla Festives) ───────────────────────────────────────

		/// <summary>Lista los festivos de un año civil (fechas ISO yyyy-MM-dd).</summary>
		[HttpGet("festives")]
		public async Task<ActionResult<DiamondFestiveYearModel>> ListFestives([FromQuery] int year)
		{
			int resolvedYear = year;
			if (resolvedYear < 1900 || resolvedYear > 2200)
			{
				resolvedYear = DateTime.Today.Year;
			}

			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				FestiveStore store = new FestiveStore(almacen);
				IReadOnlyList<DateTime> dates = await store.ListYearAsync(resolvedYear);
				DiamondFestiveYearModel model = new DiamondFestiveYearModel();
				model.Year = resolvedYear;
				int i = 0;
				while (i < dates.Count)
				{
					model.Dates.Add(FestiveStore.ToIsoDate(dates[i]));
					i++;
				}

				return model;
			}
		}

		/// <summary>Consulta si una fecha civil es festiva.</summary>
		[HttpGet("isfestive")]
		public async Task<ActionResult<bool>> IsFestive([FromQuery] string date)
		{
			DateTime day;
			if (!FestiveStore.TryParseIsoDate(date, out day))
			{
				return BadRequest("Fecha no válida (use yyyy-MM-dd).");
			}

			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				FestiveStore store = new FestiveStore(almacen);
				return await store.IsFestiveAsync(day);
			}
		}

		/// <summary>Marca o desmarca un día festivo en la tabla Festives.</summary>
		[HttpPost("setfestive")]
		public async Task<DiamondFestiveSetResult> SetFestive([FromBody] DiamondFestiveSetRequest request)
		{
			DiamondFestiveSetResult result = new DiamondFestiveSetResult();
			if (request is null)
			{
				result.Success = false;
				result.Message = "Petición vacía.";
				return result;
			}

			DateTime day;
			if (!FestiveStore.TryParseIsoDate(request.Date, out day))
			{
				result.Success = false;
				result.Message = "Fecha no válida (use yyyy-MM-dd).";
				result.Date = request.Date ?? string.Empty;
				return result;
			}

			try
			{
				using (DataStorage almacen = new DataStorage(mvarConfig))
				{
					FestiveStore store = new FestiveStore(almacen);
					await store.SetAsync(day, request.Festive);
					result.Success = true;
					result.Date = FestiveStore.ToIsoDate(day);
					result.Festive = request.Festive;
					result.Message = request.Festive
						? "Marcado como festivo."
						: "Ya no es festivo.";
					return result;
				}
			}
			catch (Exception ex)
			{
				result.Success = false;
				result.Date = FestiveStore.ToIsoDate(day);
				result.Festive = request.Festive;
				result.Message = "No se pudo guardar el festivo: " + ex.Message;
				return result;
			}
		}

		/// <summary>Hash y fecha del places.xml del servidor (sin payload).</summary>
		[HttpGet("placesheader")]
		public ActionResult<PlacesCatalogHeaderModel> GetPlacesHeader()
		{
			PlacesCatalogHeaderModel header = PlacesCatalogStore.GetHeader(mvarConfig);
			if (!header.Exists)
				return NotFound();
			return header;
		}

		/// <summary>XML del catálogo de lugares (editor Zafiro y tren).</summary>
		[HttpGet("placesxml")]
		public ActionResult<PlacesCatalogContentModel> GetPlacesXml()
		{
			PlacesCatalogContentModel? content = PlacesCatalogStore.ReadContent(mvarConfig);
			if (content is null)
				return NotFound();
			return content;
		}

		/// <summary>
		/// Guarda places.xml. Requiere sesión de ingeniero o root.
		/// Si el hash no cambia, no se escribe ni se registra en el log.
		/// </summary>
		[HttpPost("saveplaces")]
		public async Task<PlacesCatalogSaveResult> SavePlaces([FromBody] PlacesCatalogSaveRequest? request)
		{
			PlacesCatalogSaveResult result = new PlacesCatalogSaveResult();
			if (request is null || string.IsNullOrWhiteSpace(request.Xml))
			{
				result.Success = false;
				result.Message = "No se ha recibido el documento.";
				return result;
			}

			if (Guid.Empty.Equals(request.SessionToken))
			{
				result.Success = false;
				result.Message = "Se requiere una sesión activa.";
				return result;
			}

			bool canEdit =
				await hasBasicPermission(request.SessionToken, Common.UserRole.Engineer)
				|| await hasBasicPermission(request.SessionToken, Common.UserRole.Root);
			if (!canEdit)
			{
				result.Success = false;
				result.Message = "No tiene permiso para editar el catálogo de lugares.";
				return result;
			}

			IReadOnlyList<PlacesXmlIssue> issues = PlacesCatalogStore.ValidateXml(request.Xml);
			if (issues.Count > 0)
			{
				result.Success = false;
				result.Errors = issues.ToList();
				result.Message = issues.Count == 1
					? issues[0].Message
					: string.Format(
						System.Globalization.CultureInfo.InvariantCulture,
						"No se ha actualizado el catálogo: {0} errores de formato.",
						issues.Count);
				return result;
			}

			PlacesCatalogHeaderModel current = PlacesCatalogStore.GetHeader(mvarConfig);
			byte[] incoming = System.Text.Encoding.UTF8.GetBytes(request.Xml);
			string incomingHash = PlacesCatalogStore.Sha256Hex(incoming);
			if (current.Exists
				&& string.Equals(current.ContentHash, incomingHash, StringComparison.OrdinalIgnoreCase))
			{
				result.Success = true;
				result.Changed = false;
				result.ContentHash = current.ContentHash;
				result.UpdatedUtc = current.UpdatedUtc;
				result.Message = "Sin cambios.";
				return result;
			}

			PlacesCatalogHeaderModel saved = PlacesCatalogStore.WriteXml(mvarConfig, request.Xml);
			result.Success = true;
			result.Changed = true;
			result.ContentHash = saved.ContentHash;
			result.UpdatedUtc = saved.UpdatedUtc;
			result.Message = "Catálogo de lugares guardado.";

			User? actor = await retrieveSessionUser(request.SessionToken);
			if (actor is not null)
			{
				string detail = string.Format(
					System.Globalization.CultureInfo.InvariantCulture,
					"places.xml bytes={0} hash={1}",
					saved.ByteLength,
					saved.ContentHash.Length > 12 ? saved.ContentHash.Substring(0, 12) : saved.ContentHash);
				await addLoginRecord(
					actor.Id,
					Common.sessionEventType.placesCatalogEdited,
					TruncateHostPoint(detail));
			}

			return result;
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
