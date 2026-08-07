using System.Security.Cryptography;
using System.Text;
using Diamond.Timed;
using Microsoft.EntityFrameworkCore;
using Sapphire2025Models.Diamond;
using Sapphire2026.Data.Models.Diamond;

namespace Sapphire2026.Data.Diamond
{
	/// <summary>
	/// Persistencia de planes de explotación (scripts Diamond) anclados a una topología del almacén.
	/// </summary>
	public class DiamondPlanStore
	{
		public const int MaxScriptBytes = 4 * 1024 * 1024;

		private readonly DataStorage mvarContext;

		public DiamondPlanStore(DataStorage context)
		{
			mvarContext = context ?? throw new ArgumentNullException(nameof(context));
		}

		public static string ComputeContentHash(string script)
		{
			byte[] bytes = Encoding.UTF8.GetBytes(script ?? string.Empty);
			byte[] hash = SHA256.HashData(bytes);
			return Convert.ToHexString(hash);
		}

		public async Task<int> CountActiveByTopoAsync(
			Guid topoId,
			CancellationToken cancellationToken = default)
		{
			return await mvarContext.DiamondPlans
				.AsNoTracking()
				.CountAsync(x => x.TopoId == topoId && x.IsActive, cancellationToken);
		}

		public async Task<IReadOnlyList<DiamondPlanHeaderModel>> ListHeadersAsync(
			bool activeOnly = true,
			Guid? topoId = null,
			CancellationToken cancellationToken = default)
		{
			IQueryable<DiamondPlanDocument> query = mvarContext.DiamondPlans
				.AsNoTracking()
				.Include(x => x.Topo);

			if (activeOnly)
			{
				query = query.Where(x => x.IsActive);
			}

			if (topoId.HasValue && !Guid.Empty.Equals(topoId.Value))
			{
				query = query.Where(x => x.TopoId == topoId.Value);
			}

			List<DiamondPlanDocument> rows = await query
				.OrderByDescending(x => x.UpdatedUtc)
				.ToListAsync(cancellationToken);

			List<DiamondPlanHeaderModel> salida = new List<DiamondPlanHeaderModel>(rows.Count);
			int i = 0;
			while (i < rows.Count)
			{
				salida.Add(ToHeader(rows[i], includeScript: false));
				i++;
			}

			return salida;
		}

		public async Task<DiamondPlanHeaderModel?> GetAsync(
			Guid id,
			bool includeScript = true,
			CancellationToken cancellationToken = default)
		{
			DiamondPlanDocument? row = await mvarContext.DiamondPlans
				.AsNoTracking()
				.Include(x => x.Topo)
				.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
			if (row is null)
			{
				return null;
			}

			return ToHeader(row, includeScript);
		}

		public async Task<DiamondPlanSaveResult> SaveAsync(
			DiamondPlanSaveRequest request,
			CancellationToken cancellationToken = default)
		{
			DiamondPlanSaveResult result = new DiamondPlanSaveResult();
			if (request is null)
			{
				result.Success = false;
				result.Message = "Petición vacía.";
				return result;
			}

			if (Guid.Empty.Equals(request.TopoId))
			{
				result.Success = false;
				result.Message = "Debe indicar la topología del almacén (TopoId).";
				return result;
			}

			string script = request.SourceScript ?? string.Empty;
			if (string.IsNullOrWhiteSpace(script))
			{
				result.Success = false;
				result.Message = "El script del plan está vacío.";
				return result;
			}

			byte[] scriptBytes = Encoding.UTF8.GetBytes(script);
			if (scriptBytes.Length > MaxScriptBytes)
			{
				result.Success = false;
				result.Message = string.Format(
					"El script supera el tamaño máximo ({0} bytes).",
					MaxScriptBytes);
				return result;
			}

			DiamondTopoDocument? topo = await mvarContext.DiamondTopos
				.FirstOrDefaultAsync(x => x.Id == request.TopoId, cancellationToken);
			if (topo is null)
			{
				result.Success = false;
				result.Message = "La topología indicada no existe en el almacén.";
				return result;
			}

			if (!topo.IsActive)
			{
				result.Success = false;
				result.Message = "La topología indicada está inactiva; reactive o elija otra.";
				return result;
			}

			string contentHash = ComputeContentHash(script);
			string planNameFromScript = string.Empty;
			string includedPath = string.Empty;
			try
			{
				DemandCompileResult parsed = DemandScriptParser.Parse(script);
				if (parsed.PlanName.Length > 0)
				{
					planNameFromScript = parsed.PlanName;
				}

				if (parsed.IncludedTopoPath.Length > 0)
				{
					includedPath = parsed.IncludedTopoPath;
				}
			}
			catch
			{
				// El script se almacena aunque no compile; el planificador lo validará al abrir.
			}

			string name = !string.IsNullOrWhiteSpace(request.Name)
				? request.Name.Trim()
				: (planNameFromScript.Length > 0
					? planNameFromScript
					: (!string.IsNullOrWhiteSpace(request.SourceFileName)
						? System.IO.Path.GetFileNameWithoutExtension(request.SourceFileName)
						: "Plan sin nombre"));

			// Deduplicar por hash de script + misma topo (alta nueva).
			if (!request.Id.HasValue || Guid.Empty.Equals(request.Id.Value))
			{
				DiamondPlanDocument? same = await mvarContext.DiamondPlans
					.FirstOrDefaultAsync(
						x => x.ContentHash == contentHash && x.TopoId == request.TopoId,
						cancellationToken);
				if (same is not null)
				{
					if (!same.IsActive)
					{
						same.IsActive = true;
						same.UpdatedUtc = DateTime.UtcNow;
						await mvarContext.SaveChangesAsync(cancellationToken);
					}

					same = await mvarContext.DiamondPlans
						.AsNoTracking()
						.Include(x => x.Topo)
						.FirstAsync(x => x.Id == same.Id, cancellationToken);

					result.Success = true;
					result.AlreadyExists = true;
					result.Message = "Ya existía un plan idéntico (mismo script y topología).";
					result.Header = ToHeader(same, includeScript: true);
					return result;
				}

				DiamondPlanDocument doc = new DiamondPlanDocument
				{
					Id = Guid.NewGuid(),
					Name = name,
					SourceScript = script,
					ContentHash = contentHash,
					ScriptByteLength = scriptBytes.Length,
					TopoId = topo.Id,
					TopoContentHash = topo.ContentHash,
					TopoStructuralHash = topo.StructuralHash,
					IncludedTopoPath = includedPath,
					SourceFileName = request.SourceFileName ?? string.Empty,
					Author = request.Author ?? string.Empty,
					Notes = request.Notes ?? string.Empty,
					IsActive = true,
					ValidFrom = request.ValidFrom,
					CreatedUtc = DateTime.UtcNow,
					UpdatedUtc = DateTime.UtcNow
				};
				mvarContext.DiamondPlans.Add(doc);
				await mvarContext.SaveChangesAsync(cancellationToken);

				doc = await mvarContext.DiamondPlans
					.AsNoTracking()
					.Include(x => x.Topo)
					.FirstAsync(x => x.Id == doc.Id, cancellationToken);

				result.Success = true;
				result.AlreadyExists = false;
				result.Message = string.Format("Plan '{0}' almacenado.", doc.Name);
				result.Header = ToHeader(doc, includeScript: true);
				return result;
			}

			// Actualización
			DiamondPlanDocument? existing = await mvarContext.DiamondPlans
				.FirstOrDefaultAsync(x => x.Id == request.Id.Value, cancellationToken);
			if (existing is null)
			{
				result.Success = false;
				result.Message = "Plan no encontrado para actualizar.";
				return result;
			}

			existing.Name = name;
			existing.SourceScript = script;
			existing.ContentHash = contentHash;
			existing.ScriptByteLength = scriptBytes.Length;
			existing.TopoId = topo.Id;
			existing.TopoContentHash = topo.ContentHash;
			existing.TopoStructuralHash = topo.StructuralHash;
			existing.IncludedTopoPath = includedPath;
			if (!string.IsNullOrWhiteSpace(request.SourceFileName))
			{
				existing.SourceFileName = request.SourceFileName;
			}

			if (request.Author is not null)
			{
				existing.Author = request.Author;
			}

			if (request.Notes is not null)
			{
				existing.Notes = request.Notes;
			}

			if (request.ValidFrom.HasValue)
			{
				existing.ValidFrom = request.ValidFrom;
			}

			existing.UpdatedUtc = DateTime.UtcNow;
			await mvarContext.SaveChangesAsync(cancellationToken);

			existing = await mvarContext.DiamondPlans
				.AsNoTracking()
				.Include(x => x.Topo)
				.FirstAsync(x => x.Id == existing.Id, cancellationToken);

			result.Success = true;
			result.AlreadyExists = false;
			result.Message = string.Format("Plan '{0}' actualizado.", existing.Name);
			result.Header = ToHeader(existing, includeScript: true);
			return result;
		}

		public async Task<DiamondPlanSaveResult> SetActiveAsync(
			Guid id,
			bool isActive,
			CancellationToken cancellationToken = default)
		{
			DiamondPlanSaveResult result = new DiamondPlanSaveResult();
			DiamondPlanDocument? row = await mvarContext.DiamondPlans
				.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
			if (row is null)
			{
				result.Success = false;
				result.Message = "Plan no encontrado.";
				return result;
			}

			if (!isActive)
			{
				// Baja lógica libre: no bloquea topologías una vez inactivo.
				row.IsActive = false;
				row.UpdatedUtc = DateTime.UtcNow;
				await mvarContext.SaveChangesAsync(cancellationToken);
				result.Success = true;
				result.Message = "Plan desactivado.";
			}
			else
			{
				// Reactivar: la topo debe seguir activa.
				DiamondTopoDocument? topo = await mvarContext.DiamondTopos
					.AsNoTracking()
					.FirstOrDefaultAsync(x => x.Id == row.TopoId, cancellationToken);
				if (topo is null || !topo.IsActive)
				{
					result.Success = false;
					result.Message = "No se puede reactivar: la topología asociada no está disponible o está inactiva.";
					return result;
				}

				row.IsActive = true;
				row.UpdatedUtc = DateTime.UtcNow;
				await mvarContext.SaveChangesAsync(cancellationToken);
				result.Success = true;
				result.Message = "Plan activado.";
			}

			result.Header = await GetAsync(id, includeScript: false, cancellationToken);
			return result;
		}

		public static DiamondPlanHeaderModel ToHeader(DiamondPlanDocument doc, bool includeScript)
		{
			DiamondPlanHeaderModel header = new DiamondPlanHeaderModel
			{
				Id = doc.Id,
				Name = doc.Name,
				ContentHash = doc.ContentHash,
				ScriptByteLength = doc.ScriptByteLength,
				TopoId = doc.TopoId,
				TopoName = doc.Topo is not null ? doc.Topo.Name : string.Empty,
				TopoContentHash = doc.TopoContentHash,
				TopoStructuralHash = doc.TopoStructuralHash,
				IncludedTopoPath = doc.IncludedTopoPath,
				SourceFileName = doc.SourceFileName,
				Author = doc.Author,
				Notes = doc.Notes,
				IsActive = doc.IsActive,
				ValidFrom = doc.ValidFrom,
				CreatedUtc = doc.CreatedUtc,
				UpdatedUtc = doc.UpdatedUtc
			};
			if (includeScript)
			{
				header.SourceScript = doc.SourceScript;
			}

			return header;
		}
	}
}
