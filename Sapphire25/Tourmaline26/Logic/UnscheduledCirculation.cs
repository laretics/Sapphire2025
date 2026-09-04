using System.Diagnostics.CodeAnalysis;

namespace Tourmaline26.Logic
{
	/// <summary>
	/// Selección de tren sin plan Diamond ni sesión Zafiro: solo el número de servicio.
	/// </summary>
	public static class UnscheduledCirculation
	{
		public const int MaxTokenLength = 12;

		/// <summary>
		/// Número de tren tecleado a mano (no es una hora ni un destino).
		/// </summary>
		public static bool LooksLikeTrainToken([NotNullWhen(true)] string? query)
		{
			if (string.IsNullOrWhiteSpace(query))
				return false;

			string token = query.Trim();
			if (token.Length > MaxTokenLength)
				return false;

			// 8:30 / 08.30 son horas; 1234 o 8105 son números de tren.
			if (token.Contains(':') || token.Contains('.'))
				return false;

			bool anyDigit = false;
			int i = 0;
			while (i < token.Length)
			{
				char c = token[i];
				if (char.IsDigit(c))
					anyDigit = true;
				else if (!char.IsLetter(c) && c != '-' && c != '_')
					return false;
				i++;
			}

			return anyDigit;
		}

		public static string NormalizeToken(string query)
		{
			return query.Trim().ToUpperInvariant();
		}
	}
}
