using Microsoft.EntityFrameworkCore;
using Sapphire2025Models.Diamond;
using Sapphire2026.Data.Models.Diamond;

namespace Sapphire2026.Data.Diamond
{
	/// <summary>Persistencia de emisiones de documentación de circulación.</summary>
	public sealed class DiamondCirculationEmissionStore
	{
		private readonly DataStorage mvarDb;

		public DiamondCirculationEmissionStore(DataStorage db)
		{
			mvarDb = db ?? throw new ArgumentNullException(nameof(db));
		}

		public async Task<DiamondCirculationEmission> AddAsync(
			DiamondCirculationEmission emission,
			CancellationToken cancellationToken = default)
		{
			if (emission is null)
			{
				throw new ArgumentNullException(nameof(emission));
			}

			if (emission.Id == Guid.Empty)
			{
				emission.Id = Guid.NewGuid();
			}

			if (emission.EmittedAtUtc == default)
			{
				emission.EmittedAtUtc = DateTime.UtcNow;
			}

			mvarDb.DiamondCirculationEmissions.Add(emission);
			await mvarDb.SaveChangesAsync(cancellationToken);
			return emission;
		}

		public Task<DiamondCirculationEmission?> FindBySealAsync(
			string sealCode,
			CancellationToken cancellationToken = default)
		{
			string seal = CirculationSealText.Normalize(sealCode);
			if (seal.Length == 0)
			{
				return Task.FromResult<DiamondCirculationEmission?>(null);
			}

			return mvarDb.DiamondCirculationEmissions.AsNoTracking()
				.Where(e => e.SealCode.ToLower() == seal)
				.OrderByDescending(e => e.EmittedAtUtc)
				.FirstOrDefaultAsync(cancellationToken);
		}

		public Task<List<DiamondCirculationEmission>> ListRecentAsync(
			int max = 100,
			CancellationToken cancellationToken = default)
		{
			if (max < 1)
			{
				max = 1;
			}

			if (max > 1000)
			{
				max = 1000;
			}

			return mvarDb.DiamondCirculationEmissions.AsNoTracking()
				.OrderByDescending(e => e.EmittedAtUtc)
				.Take(max)
				.ToListAsync(cancellationToken);
		}
	}
}
