using System.Globalization;

namespace Sapphire2025Models.Diamond
{
	/// <summary>
	/// Numeración de consignas serie B: <c>yy/xxx</c> (año en curso y correlativo anual).
	/// </summary>
	public static class ConsignaGenerationNumbering
	{
		public static string Format(int year, int sequence)
		{
			int yy = year % 100;
			if (yy < 0)
			{
				yy = 0;
			}

			int seq = sequence < 1 ? 1 : sequence;
			return yy.ToString("00", CultureInfo.InvariantCulture)
				+ "/"
				+ seq.ToString("000", CultureInfo.InvariantCulture);
		}

		public static void ComputeNext(
			int lastYear,
			int lastSequence,
			DateTime now,
			out int year,
			out int sequence)
		{
			int currentYear = now.Year;
			if (lastYear != currentYear || lastSequence < 1)
			{
				year = currentYear;
				sequence = 1;
				return;
			}

			year = currentYear;
			sequence = lastSequence + 1;
		}

		public static string RepealText(string? previousNumber)
		{
			if (string.IsNullOrWhiteSpace(previousNumber))
			{
				return string.Empty;
			}

			return "Deroga Consigna Serie B nº " + previousNumber.Trim() + " y anteriores";
		}
	}

	/// <summary>Estado de la generación de consignas serie B (registro del almacén).</summary>
	public class DiamondConsignaGenerationStatus
	{
		public bool IsOpen { get; set; }

		/// <summary>Última consigna cerrada (<c>yy/xxx</c>), o vacío si aún no hay ninguna.</summary>
		public string LastNumber { get; set; } = string.Empty;

		/// <summary>Consigna que derogó la última cerrada.</summary>
		public string PreviousNumber { get; set; } = string.Empty;

		/// <summary>Número que se asignará al cerrar la generación abierta.</summary>
		public string NextNumber { get; set; } = string.Empty;

		public int LastYear { get; set; }

		public int LastSequence { get; set; }

		public int NextYear { get; set; }

		public int NextSequence { get; set; }

		/// <summary>
		/// Número a imprimir ahora: el de la próxima si la generación está abierta
		/// (borrador), o el de la última cerrada.
		/// </summary>
		public string NumberForDocument
		{
			get
			{
				if (IsOpen && !string.IsNullOrEmpty(NextNumber))
				{
					return NextNumber;
				}

				if (!string.IsNullOrEmpty(LastNumber))
				{
					return LastNumber;
				}

				return NextNumber;
			}
		}

		/// <summary>Número que deroga el documento que se está redactando.</summary>
		public string RepealNumber
		{
			get
			{
				if (IsOpen)
				{
					return LastNumber;
				}

				return PreviousNumber;
			}
		}
	}

	public class DiamondConsignaGenerationCloseResult
	{
		public bool Success { get; set; }

		public string Message { get; set; } = string.Empty;

		public DiamondConsignaGenerationStatus? Status { get; set; }
	}
}
