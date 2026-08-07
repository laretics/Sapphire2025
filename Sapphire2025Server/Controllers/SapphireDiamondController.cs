using System.IO.Compression;
using Diamond.Timed;
using Diamond.Topo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Sapphire2025Models.Diamond;
using Sapphire2025Server.Comunications;
using Sapphire2026.Data;
using Sapphire2026.Data.Diamond;
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

		/// <summary>Baja lógica (IsActive = false). No borra el blob.</summary>
		[HttpPost("deletetopo")]
		public async Task<DiamondTopoUploadResult> DeleteTopo([FromBody] Guid id)
		{
			DiamondTopoUploadResult result = new DiamondTopoUploadResult();
			if (Guid.Empty.Equals(id))
			{
				result.Success = false;
				result.Message = "Id vacío.";
				return result;
			}

			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				DiamondTopoStore store = new DiamondTopoStore(almacen);
				bool ok = await store.SetActiveAsync(id, false);
				if (!ok)
				{
					result.Success = false;
					result.Message = "Topología no encontrada.";
					return result;
				}

				result.Success = true;
				result.Message = "Topología desactivada.";
				result.Header = await store.GetHeaderAsync(id);
				return result;
			}
		}

		/// <summary>Reactiva una topología previamente desactivada.</summary>
		[HttpPost("activatetopo")]
		public async Task<DiamondTopoUploadResult> ActivateTopo([FromBody] Guid id)
		{
			DiamondTopoUploadResult result = new DiamondTopoUploadResult();
			if (Guid.Empty.Equals(id))
			{
				result.Success = false;
				result.Message = "Id vacío.";
				return result;
			}

			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				DiamondTopoStore store = new DiamondTopoStore(almacen);
				bool ok = await store.SetActiveAsync(id, true);
				if (!ok)
				{
					result.Success = false;
					result.Message = "Topología no encontrada.";
					return result;
				}

				result.Success = true;
				result.Message = "Topología activada.";
				result.Header = await store.GetHeaderAsync(id);
				return result;
			}
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
