using Microsoft.EntityFrameworkCore;
using Sapphire2025Models.Diamond;
using Sapphire2026.Data.Models;
using Sapphire2026.Data.Models.Diamond;

namespace Sapphire2026.Data.Diamond
{
	/// <summary>
	/// Generación de consignas serie B: estado en el registro del sistema
	/// (<c>Register</c>), sin tabla nueva.
	/// </summary>
	public class DiamondConsignaGenerationStore
	{
		internal const string KeyOpen = "Diamond.Consigna.Gen.Open";
		internal const string KeyYear = "Diamond.Consigna.Gen.Year";
		internal const string KeySeq = "Diamond.Consigna.Gen.Seq";
		internal const string KeyLast = "Diamond.Consigna.Gen.Last";
		internal const string KeyPrev = "Diamond.Consigna.Gen.Prev";

		private readonly DataStorage mvarContext;

		public DiamondConsignaGenerationStore(DataStorage context)
		{
			mvarContext = context ?? throw new ArgumentNullException(nameof(context));
		}

		public Task<DiamondConsignaGenerationStatus> GetStatusAsync(
			CancellationToken cancellationToken = default)
		{
			return GetStatusAsync(DateTime.Now, cancellationToken);
		}

		public async Task<DiamondConsignaGenerationStatus> GetStatusAsync(
			DateTime now,
			CancellationToken cancellationToken = default)
		{
			bool open = string.Equals(await ReadAsync(KeyOpen, cancellationToken), "1", StringComparison.Ordinal);
			int year = ParseInt(await ReadAsync(KeyYear, cancellationToken));
			int seq = ParseInt(await ReadAsync(KeySeq, cancellationToken));
			string last = await ReadAsync(KeyLast, cancellationToken);
			string prev = await ReadAsync(KeyPrev, cancellationToken);
			if (string.IsNullOrEmpty(last) && year > 0 && seq > 0)
			{
				last = ConsignaGenerationNumbering.Format(year, seq);
			}

			ConsignaGenerationNumbering.ComputeNext(year, seq, now, out int nextYear, out int nextSeq);
			return new DiamondConsignaGenerationStatus
			{
				IsOpen = open,
				LastNumber = last,
				PreviousNumber = prev,
				LastYear = year,
				LastSequence = seq,
				NextYear = nextYear,
				NextSequence = nextSeq,
				NextNumber = ConsignaGenerationNumbering.Format(nextYear, nextSeq)
			};
		}

		public async Task EnsureOpenAsync(CancellationToken cancellationToken = default)
		{
			string open = await ReadAsync(KeyOpen, cancellationToken);
			if (string.Equals(open, "1", StringComparison.Ordinal))
			{
				return;
			}

			await UpsertAsync(KeyOpen, "1", cancellationToken);
			await mvarContext.SaveChangesAsync(cancellationToken);
		}

		public async Task<DiamondConsignaGenerationCloseResult> CloseAsync(
			CancellationToken cancellationToken = default)
		{
			return await CloseAsync(DateTime.Now, cancellationToken);
		}

		public async Task<DiamondConsignaGenerationCloseResult> CloseAsync(
			DateTime now,
			CancellationToken cancellationToken = default)
		{
			DiamondConsignaGenerationCloseResult result = new DiamondConsignaGenerationCloseResult();
			DiamondConsignaGenerationStatus status = await GetStatusAsync(now, cancellationToken);
			if (!status.IsOpen)
			{
				result.Success = false;
				result.Message = "No hay una generación abierta. Edite la tabla de limitaciones para abrirla.";
				result.Status = status;
				return result;
			}

			List<DiamondTemporaryLimit> news = await mvarContext.DiamondTemporaryLimits
				.Where(x => x.IsNewCreation)
				.ToListAsync(cancellationToken);
			int i = 0;
			while (i < news.Count)
			{
				news[i].IsNewCreation = false;
				i++;
			}

			string issued = status.NextNumber;
			await UpsertAsync(KeyOpen, "0", cancellationToken);
			await UpsertAsync(KeyYear, status.NextYear.ToString(System.Globalization.CultureInfo.InvariantCulture), cancellationToken);
			await UpsertAsync(KeySeq, status.NextSequence.ToString(System.Globalization.CultureInfo.InvariantCulture), cancellationToken);
			await UpsertAsync(KeyPrev, status.LastNumber ?? string.Empty, cancellationToken);
			await UpsertAsync(KeyLast, issued, cancellationToken);
			await mvarContext.SaveChangesAsync(cancellationToken);

			result.Success = true;
			result.Message = "Generación " + issued + " cerrada. Las limitaciones nuevas ya no llevan marca «nueva».";
			result.Status = await GetStatusAsync(now, cancellationToken);
			return result;
		}

		private async Task<string> ReadAsync(string key, CancellationToken cancellationToken)
		{
			Register? row = await mvarContext.Register
				.AsNoTracking()
				.FirstOrDefaultAsync(x => x.Key == key, cancellationToken);
			if (row is null || row.Value is null)
			{
				return string.Empty;
			}

			return row.Value;
		}

		private async Task UpsertAsync(string key, string value, CancellationToken cancellationToken)
		{
			Register? row = await mvarContext.Register
				.FirstOrDefaultAsync(x => x.Key == key, cancellationToken);
			if (row is null)
			{
				row = new Register();
				row.Key = key;
				row.Value = value ?? string.Empty;
				mvarContext.Register.Add(row);
				return;
			}

			row.Value = value ?? string.Empty;
		}

		private static int ParseInt(string text)
		{
			int n;
			if (int.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out n))
			{
				return n;
			}

			return 0;
		}
	}
}
