using System.Globalization;
using System.Text;

namespace Tourmaline26.Services.Catalog
{
	internal static class PlaceNameText
	{
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
