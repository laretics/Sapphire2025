using System.Globalization;

namespace Sapphire2025Models.I18n
{
	/// <summary>
	/// Composición de textos de Telegram. Los argumentos con prefijo § se resuelven
	/// en el catálogo; el resto (nombres, números de tren, notas) se deja tal cual.
	/// </summary>
	public static class TelegramI18n
	{
		public const char TokenPrefix = '§';

		public static string Token(string key) => TokenPrefix + key;

		public static string T(UiLocale locale, string key, params object?[] args)
		{
			string fmt = UiCatalog.Get(locale, key);
			if (args is null || args.Length == 0)
				return fmt;
			object[] resolved = new object[args.Length];
			for (int i = 0; i < args.Length; i++)
				resolved[i] = ResolveArg(locale, args[i]);
			try
			{
				return string.Format(CultureInfo.InvariantCulture, fmt, resolved);
			}
			catch (FormatException)
			{
				return fmt;
			}
		}

		public static string ResolveArg(UiLocale locale, object? arg)
		{
			string text = arg?.ToString() ?? string.Empty;
			if (text.Length > 1 && text[0] == TokenPrefix)
				return UiCatalog.Get(locale, text.Substring(1));
			return text;
		}

		public static bool IsCancel(string? text)
		{
			string t = NormalizeReply(text);
			return t is "cancelar" or "cancellar" or "cancel·lar" or "cancel" or "sortir" or "salir" or "exit" or "no";
		}

		public static bool IsAffirmative(string? text)
		{
			string t = NormalizeReply(text);
			return t is "s" or "si" or "sí" or "yes" or "y" or "ok" or "vale"
				or "dacord" or "d'acord" or "deacuerdo" or "de acuerdo"
				or "correcte" or "correcto"
				|| t.StartsWith("si", StringComparison.Ordinal)
				|| t.StartsWith("sí", StringComparison.Ordinal)
				|| t.StartsWith("yes", StringComparison.Ordinal)
				|| t.StartsWith("ok", StringComparison.Ordinal);
		}

		private static string NormalizeReply(string? text)
		{
			if (string.IsNullOrWhiteSpace(text))
				return string.Empty;
			return text.Trim().ToLowerInvariant();
		}

		public static string ApplyPlaceholders(string phrase, IReadOnlyDictionary<string, string> parameters)
		{
			if (string.IsNullOrEmpty(phrase) || parameters is null || parameters.Count == 0)
				return phrase;
			foreach (KeyValuePair<string, string> pair in parameters)
				phrase = phrase.Replace("#" + pair.Key, pair.Value);
			return phrase;
		}
	}
}
