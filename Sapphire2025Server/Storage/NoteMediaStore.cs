using System.Globalization;

namespace Sapphire2025Server.Storage
{
	/// <summary>
	/// Almacén de adjuntos de notas: {root}/yyyy/MM/dd/{guid}.{ext}
	/// </summary>
	public static class NoteMediaStore
	{
		public const long DefaultMaxFileBytes = 20L * 1024 * 1024;
		public const int DefaultMaxUploadsPerUserPerDay = 20;

		private static readonly HashSet<string> scolAllowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			"jpg", "jpeg", "png", "gif", "webp",
			"mp4", "webm",
			"pdf"
		};

		public static string GetRoot(IConfiguration config)
		{
			string? configured = config["NoteMedia:RootPath"];
			if (string.IsNullOrWhiteSpace(configured))
				return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "App_Data", "NoteMedia"));

			// Absoluta: úsala tal cual. Relativa: anclar a BaseDirectory, no al CWD.
			if (Path.IsPathRooted(configured))
				return Path.GetFullPath(configured);

			return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configured));
		}

		public static long GetMaxFileBytes(IConfiguration config)
		{
			string? raw = config["NoteMedia:MaxFileBytes"];
			if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value) && value > 0)
				return value;
			return DefaultMaxFileBytes;
		}

		public static int GetMaxUploadsPerUserPerDay(IConfiguration config)
		{
			string? raw = config["NoteMedia:MaxUploadsPerUserPerDay"];
			if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value > 0)
				return value;
			return DefaultMaxUploadsPerUserPerDay;
		}

		public static bool IsAllowedExtension(string? ext)
		{
			return !string.IsNullOrWhiteSpace(ext) && scolAllowed.Contains(NormalizeExt(ext));
		}

		public static string NormalizeExt(string ext)
		{
			string clean = (ext ?? string.Empty).Trim().TrimStart('.');
			if (string.Equals(clean, "jpeg", StringComparison.OrdinalIgnoreCase))
				return "jpg";
			return clean.ToLowerInvariant();
		}

		public static string GuessContentType(string ext)
		{
			return NormalizeExt(ext) switch
			{
				"jpg" => "image/jpeg",
				"png" => "image/png",
				"gif" => "image/gif",
				"webp" => "image/webp",
				"mp4" => "video/mp4",
				"webm" => "video/webm",
				"pdf" => "application/pdf",
				_ => "application/octet-stream"
			};
		}

		public static bool IsImage(string? ext)
		{
			string e = NormalizeExt(ext ?? string.Empty);
			return e is "jpg" or "png" or "gif" or "webp";
		}

		public static bool IsVideo(string? ext)
		{
			string e = NormalizeExt(ext ?? string.Empty);
			return e is "mp4" or "webm";
		}

		/// <summary>Etiqueta corta para el texto del aviso (una foto, un vídeo…).</summary>
		public static string KindLabel(string? ext)
		{
			string e = NormalizeExt(ext ?? string.Empty);
			if (IsImage(e)) return "una foto";
			if (e == "mp4" || e == "webm") return "un vídeo";
			if (e == "pdf") return "un PDF";
			return "un archivo";
		}

		/// <summary>Cómo debe enviarlo Telegram. Las fotos &gt; 10 MB van como documento.</summary>
		public static string TelegramKind(string? ext, long fileBytes = 0)
		{
			string e = NormalizeExt(ext ?? string.Empty);
			if (e == "gif")
				return "animation";
			if (IsImage(e))
				return fileBytes > 10L * 1024 * 1024 ? "document" : "photo";
			if (e == "mp4")
				return "video";
			return "document";
		}

		public static string BuildPath(IConfiguration config, DateTime stampUtc, Guid id, string ext)
		{
			DateTime day = stampUtc.Kind == DateTimeKind.Utc ? stampUtc : DateTime.SpecifyKind(stampUtc, DateTimeKind.Utc);
			string root = GetRoot(config);
			return Path.Combine(
				root,
				day.ToString("yyyy", CultureInfo.InvariantCulture),
				day.ToString("MM", CultureInfo.InvariantCulture),
				day.ToString("dd", CultureInfo.InvariantCulture),
				id.ToString("N") + "." + NormalizeExt(ext));
		}

		public static void EnsureDirectory(string filePath)
		{
			string? dir = Path.GetDirectoryName(filePath);
			if (!string.IsNullOrEmpty(dir))
				Directory.CreateDirectory(dir);
		}
	}
}
