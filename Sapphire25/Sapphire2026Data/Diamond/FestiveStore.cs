using Microsoft.EntityFrameworkCore;
using Sapphire2025Models.Diamond;
using Sapphire2026.Data.Models;

namespace Sapphire2026.Data.Diamond
{
	/// <summary>
	/// Persistencia de días festivos en la tabla <c>Festives</c> (fecha civil, sin hora).
	/// Compartida por Expert y el almacén Diamond.
	/// </summary>
	public class FestiveStore
	{
		private readonly DataStorage mvarContext;

		public FestiveStore(DataStorage context)
		{
			mvarContext = context ?? throw new ArgumentNullException(nameof(context));
		}

		public static DateTime NormalizeDate(DateTime value)
		{
			return FestiveDate.Normalize(value);
		}

		public static string ToIsoDate(DateTime value)
		{
			return FestiveDate.ToIso(value);
		}

		public static bool TryParseIsoDate(string? text, out DateTime date)
		{
			return FestiveDate.TryParseIso(text, out date);
		}

		public async Task<IReadOnlyList<DateTime>> ListAsync(
			DateTime fromInclusive,
			DateTime toExclusive,
			CancellationToken cancellationToken = default)
		{
			DateTime from = NormalizeDate(fromInclusive);
			DateTime to = NormalizeDate(toExclusive);
			if (to <= from)
			{
				return Array.Empty<DateTime>();
			}

			List<Festive> rows = await mvarContext.Festives
				.AsNoTracking()
				.Where(x => x.Date >= from && x.Date < to)
				.OrderBy(x => x.Date)
				.ToListAsync(cancellationToken);

			List<DateTime> salida = new List<DateTime>(rows.Count);
			int i = 0;
			while (i < rows.Count)
			{
				salida.Add(NormalizeDate(rows[i].Date));
				i++;
			}

			return salida;
		}

		public async Task<IReadOnlyList<DateTime>> ListYearAsync(
			int year,
			CancellationToken cancellationToken = default)
		{
			if (year < 1900 || year > 2200)
			{
				return Array.Empty<DateTime>();
			}

			return await ListAsync(
				new DateTime(year, 1, 1),
				new DateTime(year + 1, 1, 1),
				cancellationToken);
		}

		public async Task<bool> IsFestiveAsync(
			DateTime day,
			CancellationToken cancellationToken = default)
		{
			DateTime start = NormalizeDate(day);
			DateTime end = start.AddDays(1);
			return await mvarContext.Festives
				.AsNoTracking()
				.AnyAsync(x => x.Date >= start && x.Date < end, cancellationToken);
		}

		public async Task<bool> SetAsync(
			DateTime day,
			bool festive,
			CancellationToken cancellationToken = default)
		{
			DateTime start = NormalizeDate(day);
			DateTime end = start.AddDays(1);
			Festive? existing = await mvarContext.Festives
				.Where(x => x.Date >= start && x.Date < end)
				.FirstOrDefaultAsync(cancellationToken);

			if (festive)
			{
				if (existing is not null)
				{
					return true;
				}

				Festive row = new Festive();
				row.Date = start;
				mvarContext.Festives.Add(row);
			}
			else
			{
				if (existing is null)
				{
					return true;
				}

				mvarContext.Festives.Remove(existing);
			}

			await mvarContext.SaveChangesAsync(cancellationToken);
			return true;
		}
	}
}
