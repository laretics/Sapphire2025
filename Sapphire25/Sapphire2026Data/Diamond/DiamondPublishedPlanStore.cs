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

			// Misma infraestructura que MeshPlannerWorkspace / DemoMeshService:
			// cantones en estaciones principales y doble vía Palma–Enllaç en T3.
			// Sin esto, la malla ve un único cantón de vía única y genera miles de
			// "errores duros" que el planificador UI no muestra (allí sí se aplica).
			SfmDemoInfrastructure.Apply(layout);

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
			else if (!ReferenceEquals(plan.Topo, layout))
			{
				// El include resolvió otra instancia (disco / almacén): aplicar la misma
				// infraestructura para que la publicación coincida con el planificador.
				SfmDemoInfrastructure.Apply(plan.Topo);
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

			// Vigencia: petición → metadatos del plan de autoría → hoy UTC.
			DateTime validFrom = DateTime.UtcNow.Date;
			if (request.ValidFrom.HasValue)
			{
				validFrom = request.ValidFrom.Value.Date;
			}
			else if (sourcePlanId.HasValue)
			{
				DiamondPlanDocument? srcMeta = await mvarContext.DiamondPlans
					.AsNoTracking()
					.FirstOrDefaultAsync(x => x.Id == sourcePlanId.Value, cancellationToken);
				if (srcMeta is not null && srcMeta.ValidFrom.HasValue)
				{
					validFrom = srcMeta.ValidFrom.Value.Date;
				}
			}

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

			bool inProduction = request.InProduction;

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
				IsActive = inProduction,
				CreatedUtc = DateTime.UtcNow
			};

			mvarContext.DiamondPublishedPlans.Add(doc);
			await mvarContext.SaveChangesAsync(cancellationToken);

			doc = await mvarContext.DiamondPublishedPlans
				.AsNoTracking()
				.Include(x => x.Topo)
				.FirstAsync(x => x.Id == doc.Id, cancellationToken);

			result.Success = true;
			string hardSummary = string.Empty;
			int noteIndex = 0;
			while (noteIndex < package.Notes.Count)
			{
				string note = package.Notes[noteIndex];
				if (note.IndexOf("error(es) duros", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					hardSummary = " · " + note.TrimEnd('.');
					break;
				}

				noteIndex++;
			}

			result.Message = string.Format(
				"Publicado «{0}» · {1} circulaciones · vigencia desde {2:d}{3}{4}.",
				doc.Name,
				doc.CirculationCount,
				doc.ValidFrom,
				doc.ValidTo.HasValue ? " hasta " + doc.ValidTo.Value.ToString("d") : string.Empty,
				hardSummary);
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
			result.Message = isActive
				? "Publicación puesta en producción."
				: "Publicación retirada de producción.";
			result.Header = await GetHeaderAsync(id, cancellationToken);
			return result;
		}

		/// <summary>Actualiza metadatos CRUD sin recompilar el payload.</summary>
		public async Task<DiamondPublishPlanResult> UpdateAsync(
			DiamondPublishedPlanUpdateRequest request,
			CancellationToken cancellationToken = default)
		{
			DiamondPublishPlanResult result = new DiamondPublishPlanResult();
			if (request is null || Guid.Empty.Equals(request.Id))
			{
				result.Success = false;
				result.Message = "Id de publicación vacío.";
				return result;
			}

			DiamondPublishedPlanDocument? row = await mvarContext.DiamondPublishedPlans
				.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
			if (row is null)
			{
				result.Success = false;
				result.Message = "Publicación no encontrada.";
				return result;
			}

			if (!string.IsNullOrWhiteSpace(request.Name))
			{
				row.Name = request.Name.Trim();
			}

			if (request.ValidFrom.HasValue)
			{
				row.ValidFrom = request.ValidFrom.Value.Date;
			}

			if (request.ValidTo.HasValue)
			{
				// Fecha sentinela muy lejana = limpiar fin
				row.ValidTo = request.ValidTo.Value.Date;
			}

			// Permitir borrar ValidTo enviando DateTime.MinValue como convención
			// (UI envía null para no tocar; para borrar usamos un flag en Notes no —
			// mejor: si ValidTo se envía como default(DateTime) no. La UI enviará
			// clearValidTo por separado si hace falta. Por ahora solo set si HasValue.

			if (request.InProduction.HasValue)
			{
				row.IsActive = request.InProduction.Value;
			}

			if (request.Notes is not null)
			{
				row.Notes = request.Notes.Length > 8000
					? request.Notes.Substring(0, 8000) + "…"
					: request.Notes;
			}

			if (row.ValidTo.HasValue && row.ValidTo.Value < row.ValidFrom)
			{
				result.Success = false;
				result.Message = "ValidTo no puede ser anterior a ValidFrom.";
				return result;
			}

			await mvarContext.SaveChangesAsync(cancellationToken);
			result.Success = true;
			result.Message = "Publicación actualizada.";
			result.Header = await GetHeaderAsync(request.Id, cancellationToken);
			return result;
		}

		/// <summary>
		/// Planes en producción asociados a una topología, cuya vigencia no ha
		/// terminado antes de <paramref name="fromDate"/> (actuales y próximos).
		/// </summary>
		public async Task<IReadOnlyList<DiamondPublishedPlanHeaderModel>> ListForTopoAsync(
			Guid topoId,
			DateTime fromDate,
			bool inProductionOnly = true,
			CancellationToken cancellationToken = default)
		{
			DateTime day = fromDate.Date;
			IQueryable<DiamondPublishedPlanDocument> query = mvarContext.DiamondPublishedPlans
				.AsNoTracking()
				.Include(x => x.Topo)
				.Where(x => x.TopoId == topoId
					&& (x.ValidTo == null || x.ValidTo >= day));

			if (inProductionOnly)
			{
				query = query.Where(x => x.IsActive);
			}

			List<DiamondPublishedPlanDocument> rows = await query
				.OrderBy(x => x.ValidFrom)
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
		/// Paquete dispositivo: metadatos de topo + planes en producción vigentes/próximos.
		/// </summary>
		public async Task<DiamondDeviceTopoPackageModel?> BuildDevicePackageAsync(
			Guid topoId,
			DateTime? fromDate = null,
			CancellationToken cancellationToken = default)
		{
			DiamondTopoDocument? topo = await mvarContext.DiamondTopos
				.AsNoTracking()
				.FirstOrDefaultAsync(x => x.Id == topoId, cancellationToken);
			if (topo is null)
			{
				return null;
			}

			DateTime day = (fromDate ?? DateTime.UtcNow).Date;
			IReadOnlyList<DiamondPublishedPlanHeaderModel> plans =
				await ListForTopoAsync(topoId, day, inProductionOnly: true, cancellationToken);

			return new DiamondDeviceTopoPackageModel
			{
				TopoId = topo.Id,
				TopoName = topo.Name,
				TopoContentHash = topo.ContentHash,
				TopoStructuralHash = topo.StructuralHash,
				TopoFormat = topo.Format,
				TopoByteLength = topo.ByteLength,
				GeneratedUtc = DateTime.UtcNow,
				FromDate = day,
				ProductionPlans = new List<DiamondPublishedPlanHeaderModel>(plans)
			};
		}

		public async Task<DiamondPublishPlanResult> DeleteAsync(
			Guid id,
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

			if (row.IsActive)
			{
				result.Success = false;
				result.Message = "No se puede borrar un plan en producción. Retírelo primero.";
				return result;
			}

			mvarContext.DiamondPublishedPlans.Remove(row);
			await mvarContext.SaveChangesAsync(cancellationToken);
			result.Success = true;
			result.Message = "Publicación eliminada del histórico.";
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
