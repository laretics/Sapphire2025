namespace Sapphire2025Models.I18n
{
	/// <summary>Idiomas de interfaz soportados.</summary>
	public enum UiLocale
	{
		Catalan = 0,
		Spanish = 1,
		English = 2
	}

	public static class UiLocales
	{
		public const string CatalanCode = "ca-IB";
		public const string SpanishCode = "es-ES";
		public const string EnglishCode = "en";

		public static UiLocale Parse(string? raw)
		{
			if (string.IsNullOrWhiteSpace(raw))
				return UiLocale.Spanish;

			string t = raw.Trim();
			if (t.StartsWith("ca", StringComparison.OrdinalIgnoreCase)
				|| t.StartsWith("cat", StringComparison.OrdinalIgnoreCase))
				return UiLocale.Catalan;
			if (t.StartsWith("en", StringComparison.OrdinalIgnoreCase))
				return UiLocale.English;
			return UiLocale.Spanish;
		}

		public static string ToCode(UiLocale locale)
		{
			return locale switch
			{
				UiLocale.Catalan => CatalanCode,
				UiLocale.English => EnglishCode,
				_ => SpanishCode
			};
		}

		public static string CultureName(UiLocale locale)
		{
			return locale switch
			{
				UiLocale.Catalan => "ca-ES",
				UiLocale.English => "en-GB",
				_ => "es-ES"
			};
		}

		public static string NativeName(UiLocale locale)
		{
			return locale switch
			{
				UiLocale.Catalan => "Català (Balear)",
				UiLocale.English => "English",
				_ => "Castellano"
			};
		}

		public static IReadOnlyList<UiLocale> All { get; } =
			new[] { UiLocale.Catalan, UiLocale.Spanish, UiLocale.English };
	}
}
