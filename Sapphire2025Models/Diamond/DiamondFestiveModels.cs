using System.Globalization;

namespace Sapphire2025Models.Diamond
{
	/// <summary>Fecha civil de festivo (ISO <c>yyyy-MM-dd</c>, sin zona).</summary>
	public static class FestiveDate
	{
		public const string IsoFormat = "yyyy-MM-dd";

		public static DateTime Normalize(DateTime value)
		{
			DateTime d = value.Date;
			return new DateTime(d.Year, d.Month, d.Day, 0, 0, 0, DateTimeKind.Unspecified);
		}

		public static string ToIso(DateTime value)
		{
			return Normalize(value).ToString(IsoFormat, CultureInfo.InvariantCulture);
		}

		public static bool TryParseIso(string? text, out DateTime date)
		{
			date = default;
			if (string.IsNullOrWhiteSpace(text))
			{
				return false;
			}

			DateTime parsed;
			if (!DateTime.TryParseExact(
				text.Trim(),
				new[] { IsoFormat, "yyyy-M-d" },
				CultureInfo.InvariantCulture,
				DateTimeStyles.None,
				out parsed))
			{
				return false;
			}

			date = Normalize(parsed);
			return true;
		}
	}

	/// <summary>Festivos de un año civil (fechas ISO <c>yyyy-MM-dd</c>).</summary>
	public class DiamondFestiveYearModel
	{
		public int Year { get; set; }

		public List<string> Dates { get; set; } = new List<string>();
	}

	/// <summary>Marca o desmarca un día como festivo.</summary>
	public class DiamondFestiveSetRequest
	{
		/// <summary>Fecha civil <c>yyyy-MM-dd</c>.</summary>
		public string Date { get; set; } = string.Empty;

		public bool Festive { get; set; }
	}

	public class DiamondFestiveSetResult
	{
		public bool Success { get; set; }

		public string Message { get; set; } = string.Empty;

		public string Date { get; set; } = string.Empty;

		public bool Festive { get; set; }
	}
}
