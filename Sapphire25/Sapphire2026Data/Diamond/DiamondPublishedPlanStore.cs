using System.Security.Cryptography;
using System.Text;
using Diamond.Project;
using Diamond.Timed;
using Diamond.Topo;
using Microsoft.EntityFrameworkCore;
using Sapphire2025Models.Diamond;
using Sapphire2026.Data.Models.Diamond;

namespace Sapphire2026.Data.Diamond
{
	/// <summary>
	/// Publicación y consulta de planes compilados para Tourmaline / explotación.
	/// </summary>
	public class DiamondPublishedPlanStore
	{
		public const int MaxPayloadBytes = 16 * 1024 * 1024;

		private readonly DataStorage mvarContext;

		public DiamondPublishedPlanStore(DataStorage context)
		{
			mvarContext = context ?? throw new ArgumentNullException(nameof(context));
		}

		public async Task<IReadOnlyList<DiamondPublishedPlanHeaderModel>> ListHeadersAsync(
			bool activeOnly = true,
			CancellationToken cancellationToken = default)
		{
			IQueryable<DiamondPublishedPlanDocument> query = mvarContext.DiamondPublishedPlans
				.AsNoTracking()
				.Include(x => x.Topo);

			if (activeOnly)
			{
				query = query.Where(x => x.IsActive);
			}

			List<DiamondPublishedPlanDocument> rows = await query
				.OrderByDescending(x => x.ValidFrom)
				.ThenByDescending(x => x.CompiledUtc)
				.ToListAsync(cancellationToken);

			List<DiamondPublishedPlanHeaderModel> salida = new List<DiamondPublishedPlanHeaderModel>(rows.Count);
			int i = 0;
			while (i < rows.Count)
			{
				salida.Add(ToHeader(rows[i]));
				i++;
			}

			return salida;
		}

		/// <summary>
		/// Plan activo vigente para la fecha civil (solo parte fecha de <paramref name="date"/>).
		/// </summary>
		public async Task<DiamondPublishedPlanHeaderModel?> GetCurrentAsync(
			DateTime date,
			CancellationToken cancellationToken = default)
		{
			DateTime day = date.Date;
			DiamondPublishedPlanDocument? row = await mvarContext.DiamondPublishedPlans
				.AsNoTracking()
				.Include(x => x.Topo)
				.Where(x => x.IsActive
					&& x.ValidFrom <= day
					&& (x.ValidTo == null || x.ValidTo >= day))
				.OrderByDescending(x => x.ValidFrom)
				.ThenByDescending(x => x.CompiledUtc)
				.FirstOrDefaultAsync(cancellationToken);

			if (row is null)
			{
				return null;
			}

			return ToHeader(row);
		}

		public async Task<DiamondPublishedPlanHeaderModel?> GetHeaderAsync(
			Guid id,
			CancellationToken cancellationToken = default)
		{
			DiamondPublishedPlanDocument? row = await mvarContext.DiamondPublishedPlans
				.AsNoTracking()
				.Include(x => x.Topo)
				.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
			if (row is null)
			{
				return null;
			}

			return ToHeader(row);
		}

		public async Task<DiamondPublishedPlanDocument?> GetDocumentAsync(
			Guid id,
			CancellationToken cancellationToken = default)
		{
			return await mvarContext.DiamondPublishedPlans
				.AsNoTracking()
				.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
		}

		public async Task<DiamondPublishPlanResult> PublishAsync(
			DiamondPublishPlanRequest request,
			CancellationToken cancellationToken = default)
		{
			DiamondPublishPlanResult result = new DiamondPublishPlanResult();
			if (request is null)
			{
				result.Success = false;
				result.Message = "Petición vacía.";
				return result;
			}

			if (Guid.Empty.Equals(request.TopoId))
			{
				result.Success = false;
				result.Message = "Debe indicar TopoId del almacén.";
				return result;
			}

			DiamondTopoDocument? topoDoc = await mvarContext.DiamondTopos
				.FirstOrDefaultAsync(x => x.Id == request.TopoId, cancellationToken);
			if (topoDoc is null)
			{
				result.Success = false;
				result.Message = "Topología no encontrada en el almacén.";
				return result;
			}

			if (!topoDoc.IsActive)
			{
				result.Success = false;
				result.Message = "La topología está inactiva; no se puede publicar sobre ella.";
				return result;
			}

			string script = request.SourceScript ?? string.Empty;
			Guid? sourcePlanId = request.SourcePlanId;
			string name = request.Name ?? string.Empty;

			if (sourcePlanId.HasValue && !Guid.Empty.Equals(sourcePlanId.Value))
			{
				DiamondPlanDocument? source = await mvarContext.DiamondPlans
					.AsNoTracking()
					.FirstOrDefaultAsync(x => x.Id == sourcePlanId.Value, cancellationToken);
				if (source is null)
				{
					result.Success = false;
					result.Message = "Plan de autoría no encontrado.";
					return result;
				}

				if (string.IsNullOrWhiteSpace(script))
				{
					script = source.SourceScript;
				}

				if (string.IsNullOrWhiteSpace(name))
				{
					name = source.Name;
				}

				// Preferir topo del plan de autoría si no se forzó otra.
				if (Guid.Empty.Equals(request.TopoId) || request.TopoId == source.TopoId)
				{
					// ok
				}
			}

			if (string.IsNullOrWhiteSpace(script))
			{
				result.Success = false;
				result.Message = "No hay script para compilar.";
				return result;
			}

			// Materializar topo XML
			byte[] xmlBytes = MaterializeXml(topoDoc.Payload, topoDoc.Format);
			TopoLayout layout;
			try
			{
				using (MemoryStream ms = new MemoryStream(xmlBytes, writable: false))
				{
					layout = TopoXmlSerializer.Load(ms);
				}
			}
			catch (Exception ex)
			{
				result.Success = false;
				result.Message = "XML de topología inválido: " + ex.Message;
				return result;
			}

			string logical = !string.IsNullOrWhiteSpace(topoDoc.SourceFileName)
				? TopoStorage.EnsureXmlExtension(topoDoc.SourceFileName)
				: TopoStorage.EnsureXmlExtension(topoDoc.Name);
			string xmlText = Encoding.UTF8.GetString(xmlBytes);
			TopoStorage.RegisterInMemory(logical, layout, xmlText);
			if (!string.IsNullOrWhiteSpace(topoDoc.Name))
			{
				TopoStorage.RegisterInMemory(topoDoc.Name, layout, xmlText);
			}

			if (!string.IsNullOrWhiteSpace(topoDoc.LayoutId))
			{
				TopoStorage.RegisterInMemory(topoDoc.LayoutId, layout, xmlText);
			}

			Plan plan = new Plan();
			plan.EnsureDefaultTrainSpecs();
			plan.Topo = layout;
			DemandCompileResult compiled = plan.CompileDemand(script, resolveStations: true);
			if (!compiled.Success)
			{
				result.Success = false;
				result.Message = "El script no compila: " + string.Join("; ", compiled.Errors);
				return result;
			}

			if (plan.Topo is null)
			{
				// Script sin include: inyectar topo del almacén.
				plan.Topo = layout;
				compiled = plan.CompileDemand(script, resolveStations: true);
				if (!compiled.Success)
				{
					result.Success = false;
					result.Message = "El script no compila con la topología: " + string.Join("; ", compiled.Errors);
					return result;
				}
			}

			if (string.IsNullOrWhiteSpace(name))
			{
				name = plan.Name.Length > 0 ? plan.Name : "Plan publicado";
			}

			PublishedProjectPackage package;
			try
			{
				package = PublishedProjectPackage.BuildFromPlan(
					plan,
					topoDoc.ContentHash,
					topoDoc.StructuralHash);
				package.Name = name.Trim();
				if (!string.IsNullOrWhiteSpace(request.Notes))
				{
					package.Notes.Insert(0, request.Notes.Trim());
				}
			}
			catch (Exception ex)
			{
				result.Success = false;
				result.Message = "Error al planificar/compilar Project: " + ex.Message;
				return result;
			}

			byte[] payload = PublishedProjectPackage.ToUtf8Json(package);
			if (payload.Length > MaxPayloadBytes)
			{
				result.Success = false;
				result.Message = "El paquete compilado supera el tamaño máximo.";
				return result;
			}

			string contentHash = Convert.ToHexString(SHA256.HashData(payload));
			DateTime validFrom = (request.ValidFrom ?? DateTime.UtcNow).Date;
			DateTime? validTo = request.ValidTo.HasValue ? request.ValidTo.Value.Date : null;
			if (validTo.HasValue && validTo.Value < validFrom)
			{
				result.Success = false;
				result.Message = "ValidTo no puede ser anterior a ValidFrom.";
				return result;
			}

			int circCount = 0;
			int asimCount = 0;
			int di = 0;
			while (di < package.Days.Count)
			{
				circCount += package.Days[di].Circulations.Count;
				asimCount += package.Days[di].Asimilations.Count;
				di++;
			}

			string notesJoined = string.Join("\n", package.Notes);
			if (notesJoined.Length > 8000)
			{
				notesJoined = notesJoined.Substring(0, 8000) + "…";
			}

			DiamondPublishedPlanDocument doc = new DiamondPublishedPlanDocument
			{
				Id = Guid.NewGuid(),
				SourcePlanId = sourcePlanId.HasValue && !Guid.Empty.Equals(sourcePlanId.Value)
					? sourcePlanId
					: null,
				Name = name.Trim(),
				TopoId = topoDoc.Id,
				TopoContentHash = topoDoc.ContentHash,
				TopoStructuralHash = topoDoc.StructuralHash,
				ValidFrom = validFrom,
				ValidTo = validTo,
				CompiledUtc = package.CompiledUtc,
				ContentHash = contentHash,
				Format = PublishedProjectPackage.FormatId,
				Payload = payload,
				ByteLength = payload.Length,
				CirculationCount = circCount,
				AsimilationCount = asimCount,
				Notes = notesJoined,
				IsActive = true,
				CreatedUtc = DateTime.UtcNow
			};

			mvarContext.DiamondPublishedPlans.Add(doc);
			await mvarContext.SaveChangesAsync(cancellationToken);

			doc = await mvarContext.DiamondPublishedPlans
				.AsNoTracking()
				.Include(x => x.Topo)
				.FirstAsync(x => x.Id == doc.Id, cancellationToken);

			result.Success = true;
			result.Message = string.Format(
				"Publicado «{0}» · {1} circulaciones · vigencia desde {2:d}{3}.",
				doc.Name,
				doc.CirculationCount,
				doc.ValidFrom,
				doc.ValidTo.HasValue ? " hasta " + doc.ValidTo.Value.ToString("d") : string.Empty);
			result.Header = ToHeader(doc);
			return result;
		}

		public async Task<DiamondPublishPlanResult> SetActiveAsync(
			Guid id,
			bool isActive,
			CancellationToken cancellationToken = default)
		{
			DiamondPublishPlanResult result = new DiamondPublishPlanResult();
			DiamondPublishedPlanDocument? row = await mvarContext.DiamondPublishedPlans
				.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
			if (row is null)
			{
				result.Success = false;
				result.Message = "Publicación no encontrada.";
				return result;
			}

			row.IsActive = isActive;
			await mvarContext.SaveChangesAsync(cancellationToken);
			result.Success = true;
			result.Message = isActive ? "Publicación reactivada." : "Publicación retirada de explotación.";
			result.Header = await GetHeaderAsync(id, cancellationToken);
			return result;
		}

		public static DiamondPublishedPlanHeaderModel ToHeader(DiamondPublishedPlanDocument doc)
		{
			return new DiamondPublishedPlanHeaderModel
			{
				Id = doc.Id,
				SourcePlanId = doc.SourcePlanId,
				Name = doc.Name,
				TopoId = doc.TopoId,
				TopoName = doc.Topo is not null ? doc.Topo.Name : string.Empty,
				TopoContentHash = doc.TopoContentHash,
				TopoStructuralHash = doc.TopoStructuralHash,
				ValidFrom = doc.ValidFrom,
				ValidTo = doc.ValidTo,
				CompiledUtc = doc.CompiledUtc,
				ContentHash = doc.ContentHash,
				Format = doc.Format,
				ByteLength = doc.ByteLength,
				CirculationCount = doc.CirculationCount,
				AsimilationCount = doc.AsimilationCount,
				Notes = doc.Notes,
				IsActive = doc.IsActive,
				CreatedUtc = doc.CreatedUtc
			};
		}

		private static byte[] MaterializeXml(byte[] payload, string format)
		{
			if (string.Equals(format, "xml-gz", StringComparison.OrdinalIgnoreCase)
				|| (payload.Length >= 2 && payload[0] == 0x1f && payload[1] == 0x8b))
			{
				using MemoryStream input = new MemoryStream(payload, writable: false);
				using System.IO.Compression.GZipStream gzip =
					new System.IO.Compression.GZipStream(input, System.IO.Compression.CompressionMode.Decompress);
				using MemoryStream output = new MemoryStream();
				gzip.CopyTo(output);
				return output.ToArray();
			}

			return payload;
		}
	}
}
