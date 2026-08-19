using Microsoft.EntityFrameworkCore;
using Sapphire2025Models.Diamond;
using Sapphire2026.Data.Models.Diamond;

namespace Sapphire2026.Data.Diamond
{
	/// <summary>
	/// Persistencia de limitaciones temporales de velocidad, ancladas a una topología.
	/// </summary>
	public class DiamondTemporaryLimitStore
	{
		private readonly DataStorage mvarContext;

		public DiamondTemporaryLimitStore(DataStorage context)
		{
			mvarContext = context ?? throw new ArgumentNullException(nameof(context));
		}

		public async Task<IReadOnlyList<DiamondTemporaryLimitModel>> ListAsync(
			Guid topoId,
			string? axisId,
			CancellationToken cancellationToken = default)
		{
			if (Guid.Empty.Equals(topoId))
			{
				return Array.Empty<DiamondTemporaryLimitModel>();
			}

			IQueryable<DiamondTemporaryLimit> query = mvarContext.DiamondTemporaryLimits
				.AsNoTracking()
				.Where(x => x.TopoId == topoId);

			if (!string.IsNullOrWhiteSpace(axisId))
			{
				string axis = axisId.Trim();
				query = query.Where(x => x.AxisId == axis);
			}

			List<DiamondTemporaryLimit> rows = await query
				.OrderBy(x => x.AxisId)
				.ThenBy(x => x.Pk0)
				.ThenBy(x => x.Pkf)
				.ToListAsync(cancellationToken);

			List<DiamondTemporaryLimitModel> salida = new List<DiamondTemporaryLimitModel>(rows.Count);
			int i = 0;
			while (i < rows.Count)
			{
				salida.Add(ToModel(rows[i]));
				i++;
			}

			return salida;
		}

		public async Task<DiamondTemporaryLimitModel?> GetAsync(
			Guid id,
			CancellationToken cancellationToken = default)
		{
			if (Guid.Empty.Equals(id))
			{
				return null;
			}

			DiamondTemporaryLimit? row = await mvarContext.DiamondTemporaryLimits
				.AsNoTracking()
				.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
			if (row is null)
			{
				return null;
			}

			return ToModel(row);
		}

		public async Task<DiamondTemporaryLimitSaveResult> SaveAsync(
			DiamondTemporaryLimitSaveRequest request,
			CancellationToken cancellationToken = default)
		{
			DiamondTemporaryLimitSaveResult result = new DiamondTemporaryLimitSaveResult();
			if (request is null)
			{
				result.Success = false;
				result.Message = "Petición vacía.";
				return result;
			}

			string? validation = Validate(request);
			if (validation is not null)
			{
				result.Success = false;
				result.Message = validation;
				return result;
			}

			DiamondTopoDocument? topo = await mvarContext.DiamondTopos
				.AsNoTracking()
				.FirstOrDefaultAsync(x => x.Id == request.TopoId, cancellationToken);
			if (topo is null)
			{
				result.Success = false;
				result.Message = "La topología indicada no existe en el almacén.";
				return result;
			}

			long pk0 = request.Pk0;
			long pkf = request.Pkf;
			if (pkf < pk0)
			{
				long swap = pk0;
				pk0 = pkf;
				pkf = swap;
			}

			if (request.Id.HasValue && !Guid.Empty.Equals(request.Id.Value))
			{
				DiamondTemporaryLimit? existing = await mvarContext.DiamondTemporaryLimits
					.FirstOrDefaultAsync(x => x.Id == request.Id.Value, cancellationToken);
				if (existing is null)
				{
					result.Success = false;
					result.Message = "No se encontró la limitación.";
					return result;
				}

				if (existing.TopoId != request.TopoId)
				{
					result.Success = false;
					result.Message = "La limitación no pertenece a esa topología.";
					return result;
				}

				existing.AxisId = request.AxisId.Trim();
				existing.Pk0 = pk0;
				existing.Pkf = pkf;
				existing.Speed = request.Speed;
				existing.Track = (byte)request.Track;
				existing.Reason = (byte)request.Reason;
				existing.SignaledOnTrack = request.SignaledOnTrack;
				existing.Observations = NormalizeObservations(request.Observations);
				await EnsureGenerationOpenAsync(cancellationToken);
				await mvarContext.SaveChangesAsync(cancellationToken);
				result.Success = true;
				result.Message = "Limitación actualizada.";
				result.Item = ToModel(existing);
				return result;
			}

			DiamondTemporaryLimit created = new DiamondTemporaryLimit
			{
				Id = Guid.NewGuid(),
				TopoId = request.TopoId,
				AxisId = request.AxisId.Trim(),
				Pk0 = pk0,
				Pkf = pkf,
				Speed = request.Speed,
				Track = (byte)request.Track,
				IsNewCreation = true,
				Reason = (byte)request.Reason,
				CreatedUtc = DateTime.UtcNow,
				SignaledOnTrack = request.SignaledOnTrack,
				Observations = NormalizeObservations(request.Observations)
			};
			mvarContext.DiamondTemporaryLimits.Add(created);
			await EnsureGenerationOpenAsync(cancellationToken);
			await mvarContext.SaveChangesAsync(cancellationToken);
			result.Success = true;
			result.Message = "Limitación creada.";
			result.Item = ToModel(created);
			return result;
		}

		public async Task<DiamondTemporaryLimitSaveResult> DeleteAsync(
			Guid id,
			CancellationToken cancellationToken = default)
		{
			DiamondTemporaryLimitSaveResult result = new DiamondTemporaryLimitSaveResult();
			if (Guid.Empty.Equals(id))
			{
				result.Success = false;
				result.Message = "Id vacío.";
				return result;
			}

			DiamondTemporaryLimit? row = await mvarContext.DiamondTemporaryLimits
				.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
			if (row is null)
			{
				result.Success = false;
				result.Message = "No se encontró la limitación.";
				return result;
			}

			mvarContext.DiamondTemporaryLimits.Remove(row);
			await EnsureGenerationOpenAsync(cancellationToken);
			await mvarContext.SaveChangesAsync(cancellationToken);
			result.Success = true;
			result.Message = "Limitación eliminada.";
			return result;
		}

		public static string? Validate(DiamondTemporaryLimitSaveRequest request)
		{
			if (Guid.Empty.Equals(request.TopoId))
			{
				return "Debe indicar la topología del almacén.";
			}

			if (string.IsNullOrWhiteSpace(request.AxisId))
			{
				return "Debe indicar el eje.";
			}

			if (request.Pk0 == request.Pkf)
			{
				return "El tramo no puede tener longitud cero.";
			}

			if (request.Speed <= 0 || request.Speed > 400)
			{
				return "La velocidad debe estar entre 1 y 400 km/h.";
			}

			if (!Enum.IsDefined(typeof(TemporaryLimitTrack), request.Track)
				|| request.Track == 0)
			{
				return "Vía no válida (1, 2 o ambas).";
			}

			if (!Enum.IsDefined(typeof(TemporaryLimitReason), request.Reason))
			{
				return "Motivo no válido.";
			}

			return null;
		}

		public static List<global::Diamond.Topo.TemporarySpeedLimit> ToTopoLimits(
			IReadOnlyList<DiamondTemporaryLimitModel> rows)
		{
			if (rows is null || rows.Count == 0)
			{
				return new List<global::Diamond.Topo.TemporarySpeedLimit>();
			}

			List<global::Diamond.Topo.TemporarySpeedLimit> salida =
				new List<global::Diamond.Topo.TemporarySpeedLimit>(rows.Count);
			int i = 0;
			while (i < rows.Count)
			{
				DiamondTemporaryLimitModel row = rows[i];
				salida.Add(global::Diamond.Topo.TopoTemporaryLimits.FromSpan(
					row.AxisId,
					row.Pk0,
					row.Pkf,
					row.Speed,
					(global::Diamond.Topo.TemporaryLimitReason)row.Reason,
					row.Observations,
					(global::Diamond.Topo.TemporaryLimitTrack)row.Track,
					row.IsNewCreation,
					row.CreatedUtc,
					row.SignaledOnTrack));
				i++;
			}

			return salida;
		}

		public static DiamondTemporaryLimitModel ToModel(DiamondTemporaryLimit row)
		{
			return new DiamondTemporaryLimitModel
			{
				Id = row.Id,
				TopoId = row.TopoId,
				AxisId = row.AxisId,
				Pk0 = row.Pk0,
				Pkf = row.Pkf,
				Speed = row.Speed,
				Track = (TemporaryLimitTrack)row.Track,
				IsNewCreation = row.IsNewCreation,
				Reason = (TemporaryLimitReason)row.Reason,
				CreatedUtc = row.CreatedUtc,
				SignaledOnTrack = row.SignaledOnTrack,
				Observations = row.Observations ?? string.Empty
			};
		}

		private async Task EnsureGenerationOpenAsync(CancellationToken cancellationToken)
		{
			DiamondConsignaGenerationStore generation = new DiamondConsignaGenerationStore(mvarContext);
			await generation.EnsureOpenAsync(cancellationToken);
		}

		private static string NormalizeObservations(string? text)
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				return string.Empty;
			}

			string trimmed = text.Trim();
			if (trimmed.Length > 500)
			{
				return trimmed.Substring(0, 500);
			}

			return trimmed;
		}
	}
}
