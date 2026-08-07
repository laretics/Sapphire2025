using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Sapphire2025Models.Diamond;
using Sapphire2026.Data.Models.Diamond;

namespace Sapphire2026.Data.Diamond
{
	/// <summary>
	/// Persistencia de topologías Diamond en la BD de Sapphire (blob + metadatos).
	/// No valida el XML: eso lo hace el servidor con <c>Diamond.Topo.TopoXmlSerializer</c>.
	/// </summary>
	public class DiamondTopoStore
	{
		public const int MaxPayloadBytes = 8 * 1024 * 1024;
		public const string FormatXml = "xml";
		public const string FormatXmlGz = "xml-gz";

		private readonly DataStorage mvarContext;

		public DiamondTopoStore(DataStorage context)
		{
			mvarContext = context ?? throw new ArgumentNullException(nameof(context));
		}

		public static string ComputeContentHash(byte[] payload)
		{
			if (payload is null)
			{
				throw new ArgumentNullException(nameof(payload));
			}

			byte[] hash = SHA256.HashData(payload);
			return Convert.ToHexString(hash);
		}

		public async Task<IReadOnlyList<DiamondTopoHeaderModel>> ListHeadersAsync(
			bool activeOnly = true,
			CancellationToken cancellationToken = default)
		{
			IQueryable<DiamondTopoDocument> query = mvarContext.DiamondTopos.AsNoTracking();
			if (activeOnly)
			{
				query = query.Where(x => x.IsActive);
			}

			List<DiamondTopoDocument> rows = await query
				.OrderByDescending(x => x.CreatedUtc)
				.ToListAsync(cancellationToken);

			List<DiamondTopoHeaderModel> salida = new List<DiamondTopoHeaderModel>(rows.Count);
			int i = 0;
			while (i < rows.Count)
			{
				salida.Add(ToHeader(rows[i]));
				i++;
			}

			return salida;
		}

		public async Task<DiamondTopoHeaderModel?> GetHeaderAsync(
			Guid id,
			CancellationToken cancellationToken = default)
		{
			DiamondTopoDocument? row = await mvarContext.DiamondTopos
				.AsNoTracking()
				.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
			if (row is null)
			{
				return null;
			}

			return ToHeader(row);
		}

		public async Task<DiamondTopoHeaderModel?> GetHeaderByContentHashAsync(
			string contentHash,
			CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrWhiteSpace(contentHash))
			{
				return null;
			}

			string normalized = contentHash.Trim().ToUpperInvariant();
			DiamondTopoDocument? row = await mvarContext.DiamondTopos
				.AsNoTracking()
				.FirstOrDefaultAsync(x => x.ContentHash == normalized, cancellationToken);
			if (row is null)
			{
				return null;
			}

			return ToHeader(row);
		}

		public async Task<DiamondTopoDocument?> GetDocumentAsync(
			Guid id,
			CancellationToken cancellationToken = default)
		{
			return await mvarContext.DiamondTopos
				.AsNoTracking()
				.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
		}

		/// <summary>
		/// Inserta un documento ya validado. Si el ContentHash existe, devuelve el existente.
		/// </summary>
		public async Task<DiamondTopoUploadResult> UpsertValidatedAsync(
			byte[] payload,
			string format,
			string name,
			string author,
			string layoutId,
			string structuralHash,
			int stationCount,
			int axisCount,
			string sourceFileName,
			string notes,
			DateTime? validFrom,
			CancellationToken cancellationToken = default)
		{
			DiamondTopoUploadResult result = new DiamondTopoUploadResult();
			if (payload is null || payload.Length == 0)
			{
				result.Success = false;
				result.Message = "Payload vacío.";
				return result;
			}

			if (payload.Length > MaxPayloadBytes)
			{
				result.Success = false;
				result.Message = string.Format(
					"El archivo supera el tamaño máximo ({0} bytes).",
					MaxPayloadBytes);
				return result;
			}

			string contentHash = ComputeContentHash(payload);
			DiamondTopoDocument? existing = await mvarContext.DiamondTopos
				.FirstOrDefaultAsync(x => x.ContentHash == contentHash, cancellationToken);
			if (existing is not null)
			{
				// Re-activar si estaba de baja y es el mismo contenido.
				if (!existing.IsActive)
				{
					existing.IsActive = true;
					await mvarContext.SaveChangesAsync(cancellationToken);
				}

				result.Success = true;
				result.AlreadyExists = true;
				result.Message = string.Format(
					"Ya existe una topología con el mismo contenido (hash {0}).",
					contentHash);
				result.Header = ToHeader(existing);
				return result;
			}

			DiamondTopoDocument doc = new DiamondTopoDocument
			{
				Id = Guid.NewGuid(),
				Name = string.IsNullOrWhiteSpace(name) ? "Sin nombre" : name.Trim(),
				ContentHash = contentHash,
				StructuralHash = structuralHash ?? string.Empty,
				Format = string.IsNullOrWhiteSpace(format) ? FormatXml : format.Trim(),
				Payload = payload,
				ByteLength = payload.Length,
				SourceFileName = sourceFileName ?? string.Empty,
				Author = author ?? string.Empty,
				LayoutId = layoutId ?? string.Empty,
				StationCount = stationCount,
				AxisCount = axisCount,
				Notes = notes ?? string.Empty,
				IsActive = true,
				ValidFrom = validFrom,
				CreatedUtc = DateTime.UtcNow
			};

			mvarContext.DiamondTopos.Add(doc);
			await mvarContext.SaveChangesAsync(cancellationToken);

			result.Success = true;
			result.AlreadyExists = false;
			result.Message = string.Format(
				"Topología '{0}' almacenada ({1} bytes).",
				doc.Name,
				doc.ByteLength);
			result.Header = ToHeader(doc);
			return result;
		}

		public async Task<bool> SetActiveAsync(
			Guid id,
			bool isActive,
			CancellationToken cancellationToken = default)
		{
			DiamondTopoDocument? row = await mvarContext.DiamondTopos
				.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
			if (row is null)
			{
				return false;
			}

			row.IsActive = isActive;
			await mvarContext.SaveChangesAsync(cancellationToken);
			return true;
		}

		public static DiamondTopoHeaderModel ToHeader(DiamondTopoDocument doc)
		{
			return new DiamondTopoHeaderModel
			{
				Id = doc.Id,
				Name = doc.Name,
				ContentHash = doc.ContentHash,
				StructuralHash = doc.StructuralHash,
				Format = doc.Format,
				ByteLength = doc.ByteLength,
				SourceFileName = doc.SourceFileName,
				Author = doc.Author,
				LayoutId = doc.LayoutId,
				StationCount = doc.StationCount,
				AxisCount = doc.AxisCount,
				Notes = doc.Notes,
				IsActive = doc.IsActive,
				ValidFrom = doc.ValidFrom,
				CreatedUtc = doc.CreatedUtc
			};
		}
	}
}
