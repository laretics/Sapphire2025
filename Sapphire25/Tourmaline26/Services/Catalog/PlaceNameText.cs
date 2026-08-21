using System.Globalization;
using System.Text;

namespace Tourmaline26.Services.Catalog
{
	internal static class PlaceNameText
	{
		private static readonly HashSet<string> Stopwords = new(StringComparer.Ordinal)
		{
			"de", "d", "del", "dels", "la", "el", "els", "les",
			"es", "sa", "s", "i", "y", "a", "al", "of", "the"
		};

		public static string Normalize(string? value)
		{
			if (string.IsNullOrWhiteSpace(value))
				return string.Empty;

			string s = value.Trim().ToLowerInvariant();
			s = RemoveDiacritics(s);
			s = s.Replace('-', ' ');
			s = s.Replace("estacio", " ", StringComparison.Ordinal)
				.Replace("estacion", " ", StringComparison.Ordinal)
				.Replace("station", " ", StringComparison.Ordinal);
			if (s.EndsWith(" int", StringComparison.Ordinal))
				s = s[..^4];
			s = s.Replace(" intermodal", " ", StringComparison.Ordinal);

			var sb = new StringBuilder(s.Length);
			bool space = false;
			foreach (char c in s)
			{
				if (char.IsLetterOrDigit(c))
				{
					sb.Append(c);
					space = false;
				}
				else if (!space)
				{
					sb.Append(' ');
					space = true;
				}
			}
			return sb.ToString().Trim();
		}

		/// <summary>
		/// Destino de lanzadera TIB: "Estació - Alaró" / "Estació - Consell - Estació"
		/// se quedan en el pueblo. No toca rutas con varios pueblos (Alaró - Orient).
		/// </summary>
		public static string CleanTransitHeadsign(string? value)
		{
			if (string.IsNullOrWhiteSpace(value))
				return string.Empty;

			string raw = value.Trim();
			string[] parts = raw.Split(['-', '/', '|', '–', '—'], StringSplitOptions.RemoveEmptyEntries);
			var towns = new List<string>();
			foreach (string part in parts)
			{
				string trimmed = part.Trim();
				if (trimmed.Length == 0)
					continue;
				if (Normalize(trimmed).Length == 0)
					continue;
				towns.Add(trimmed);
			}

			return towns.Count == 1 ? towns[0] : raw;
		}

		/// <summary>
		/// True si <paramref name="word"/> aparece como palabra completa
		/// (precedida por inicio/espacio y seguida de espacio o fin).
		/// </summary>
		public static bool ContainsWord(string? value, string word)
		{
			string haystack = Normalize(value);
			string needle = Normalize(word);
			if (haystack.Length == 0 || needle.Length == 0)
				return false;
			return ContainsNormalizedWord(haystack, needle);
		}

		/// <summary>Prefijo de palabra: igual o seguido de espacio.</summary>
		public static bool IsWordPrefix(string haystack, string prefix)
		{
			if (haystack.Length == 0 || prefix.Length == 0)
				return false;
			if (!haystack.StartsWith(prefix, StringComparison.Ordinal))
				return false;
			return haystack.Length == prefix.Length || haystack[prefix.Length] == ' ';
		}

		/// <summary>
		/// Destino de puerto: la palabra "port", no un substring
		/// (Pòrtol, aeroport…).
		/// </summary>
		public static bool IsPortWord(string? value)
		{
			string n = Normalize(value);
			if (n.Length == 0)
				return false;
			if (ContainsNormalizedWord(n, "aeroport") || ContainsNormalizedWord(n, "airport"))
				return false;
			return ContainsNormalizedWord(n, "port");
		}

		public static bool IsAirportWord(string? value)
		{
			string n = Normalize(value);
			if (n.Length == 0)
				return false;
			return ContainsNormalizedWord(n, "aeroport") || ContainsNormalizedWord(n, "airport");
		}

		/// <summary>
		/// Misma identidad de lugar: mismas palabras distintivas, en el mismo
		/// orden. "Polígon de Marratxí" ≠ "Marratxí".
		/// </summary>
		public static bool SameDistinctiveTokens(string normalizedA, string normalizedB)
		{
			List<string> a = DistinctiveTokens(normalizedA);
			List<string> b = DistinctiveTokens(normalizedB);
			if (a.Count == 0 || a.Count != b.Count)
				return false;
			for (int i = 0; i < a.Count; i++)
			{
				if (!string.Equals(a[i], b[i], StringComparison.Ordinal))
					return false;
			}
			return true;
		}

		private static List<string> DistinctiveTokens(string normalized)
		{
			var tokens = new List<string>();
			if (string.IsNullOrEmpty(normalized))
				return tokens;
			foreach (string part in normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries))
			{
				if (!Stopwords.Contains(part))
					tokens.Add(part);
			}
			return tokens;
		}

		internal static bool ContainsNormalizedWord(string haystack, string word)
		{
			int start = 0;
			while (start <= haystack.Length - word.Length)
			{
				int at = haystack.IndexOf(word, start, StringComparison.Ordinal);
				if (at < 0)
					return false;
				bool left = at == 0 || haystack[at - 1] == ' ';
				int end = at + word.Length;
				bool right = end == haystack.Length || haystack[end] == ' ';
				if (left && right)
					return true;
				start = at + 1;
			}
			return false;
		}

		private static string RemoveDiacritics(string text)
		{
			string normalized = text.Normalize(NormalizationForm.FormD);
			var sb = new StringBuilder(normalized.Length);
			foreach (char c in normalized)
			{
				if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
					sb.Append(c);
			}
			return sb.ToString().Normalize(NormalizationForm.FormC);
		}
	}
}
