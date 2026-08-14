using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Sapphire2025.Help
{
	/// <summary>
	/// Parsea el Body de un tema de ayuda en bloques renderizables.
	/// Soporta párrafos, listas con •, negrita **...** e imágenes [[img:ruta|pie]].
	/// </summary>
	public static class HelpBodyParser
	{
		/// <summary>
		/// Sintaxis de imagen (bloque o inline):
		/// [[img:img/help/ejemplo.png]]
		/// [[img:img/help/ejemplo.png|Pie de foto opcional]]
		/// </summary>
		private static readonly Regex ImageTokenRegex = new(
			@"\[\[img:\s*([^\]|]+?)(?:\s*\|\s*([^\]]*?))?\s*\]\]",
			RegexOptions.IgnoreCase | RegexOptions.Compiled);

		private static readonly Regex BoldRegex = new(
			@"\*\*(.+?)\*\*",
			RegexOptions.Compiled);

		public abstract record Block;

		public sealed record ParagraphBlock(string Html) : Block;

		public sealed record ListBlock(IReadOnlyList<string> ItemHtml) : Block;

		public sealed record ImageBlock(string Src, string? Caption, string Alt) : Block;

		public static IReadOnlyList<Block> Parse(string? body)
		{
			List<Block> result = new();
			if (string.IsNullOrWhiteSpace(body))
				return result;

			string normalized = body.Replace("\r\n", "\n").Replace("\r", "\n");
			string[] blocks = normalized.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);

			foreach (string rawBlock in blocks)
			{
				string trimmed = rawBlock.Trim();
				if (trimmed.Length == 0)
					continue;

				// Bloque que es solo una imagen (posiblemente con espacios)
				if (TryParseSoleImage(trimmed, out ImageBlock? soleImage) && soleImage is not null)
				{
					result.Add(soleImage);
					continue;
				}

				// Lista de viñetas
				string[] lines = trimmed.Split('\n', StringSplitOptions.None)
					.Select(l => l.Trim())
					.Where(l => l.Length > 0)
					.ToArray();

				if (lines.Length > 0 && lines.All(l => l.StartsWith("•") || IsSoleImageLine(l)))
				{
					// Mezcla de viñetas e imágenes: emitir en orden
					List<string> pendingBullets = new();
					void FlushBullets()
					{
						if (pendingBullets.Count == 0)
							return;
						result.Add(new ListBlock(pendingBullets.Select(FormatInlineHtml).ToList()));
						pendingBullets.Clear();
					}

					foreach (string line in lines)
					{
						if (TryParseSoleImage(line, out ImageBlock? img) && img is not null)
						{
							FlushBullets();
							result.Add(img);
						}
						else if (line.StartsWith("•"))
						{
							pendingBullets.Add(line.TrimStart('•').TrimStart());
						}
					}
					FlushBullets();
					continue;
				}

				// Párrafo: puede contener tokens de imagen inline → partir en subbloques
				foreach (Block part in SplitInlineContent(trimmed))
					result.Add(part);
			}

			return result;
		}

		private static IEnumerable<Block> SplitInlineContent(string text)
		{
			MatchCollection matches = ImageTokenRegex.Matches(text);
			if (matches.Count == 0)
			{
				yield return new ParagraphBlock(FormatInlineHtml(text));
				yield break;
			}

			int last = 0;
			foreach (Match match in matches)
			{
				if (match.Index > last)
				{
					string before = text[last..match.Index].Trim();
					if (before.Length > 0)
						yield return new ParagraphBlock(FormatInlineHtml(before));
				}

				if (TryCreateImage(match.Groups[1].Value, match.Groups[2].Success ? match.Groups[2].Value : null, out ImageBlock? img)
					&& img is not null)
				{
					yield return img;
				}

				last = match.Index + match.Length;
			}

			if (last < text.Length)
			{
				string after = text[last..].Trim();
				if (after.Length > 0)
					yield return new ParagraphBlock(FormatInlineHtml(after));
			}
		}

		private static bool IsSoleImageLine(string line) =>
			TryParseSoleImage(line, out _);

		private static bool TryParseSoleImage(string text, out ImageBlock? image)
		{
			image = null;
			string t = text.Trim();
			Match m = ImageTokenRegex.Match(t);
			if (!m.Success || m.Index != 0 || m.Length != t.Length)
				return false;
			return TryCreateImage(m.Groups[1].Value, m.Groups[2].Success ? m.Groups[2].Value : null, out image);
		}

		private static bool TryCreateImage(string rawSrc, string? rawCaption, out ImageBlock? image)
		{
			image = null;
			string? src = SanitizeImageSrc(rawSrc);
			if (src is null)
				return false;

			string? caption = string.IsNullOrWhiteSpace(rawCaption) ? null : rawCaption.Trim();
			string alt = caption ?? System.IO.Path.GetFileNameWithoutExtension(src) ?? "Ilustración de ayuda";
			image = new ImageBlock(src, caption, alt);
			return true;
		}

		/// <summary>
		/// Solo rutas relativas locales (wwwroot). Rechaza http(s), //, .., caracteres raros.
		/// </summary>
		public static string? SanitizeImageSrc(string? raw)
		{
			if (string.IsNullOrWhiteSpace(raw))
				return null;

			string src = raw.Trim().Trim('"', '\'');
			src = src.Replace('\\', '/');

			// Quitar barra inicial
			while (src.StartsWith('/'))
				src = src[1..];

			if (src.Length == 0)
				return null;

			// Prohibir URLs absolutas y protocol-relative
			if (src.Contains("://", StringComparison.Ordinal)
				|| src.StartsWith("//", StringComparison.Ordinal)
				|| src.Contains(':', StringComparison.Ordinal)) // file:, data:, etc.
				return null;

			// Path traversal
			if (src.Contains("..", StringComparison.Ordinal))
				return null;

			// Solo caracteres seguros en la ruta
			if (!Regex.IsMatch(src, @"^[A-Za-z0-9_\-./]+$"))
				return null;

			// Extensiones de imagen razonables
			string ext = System.IO.Path.GetExtension(src).ToLowerInvariant();
			if (ext is not (".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".svg" or ".bmp"))
				return null;

			return src;
		}

		public static string FormatInlineHtml(string text)
		{
			// Primero quitar tokens de imagen residuales (no deberían llegar aquí con src válido sin procesar)
			string withoutOrphanImg = ImageTokenRegex.Replace(text, string.Empty);
			string escaped = WebUtility.HtmlEncode(withoutOrphanImg);
			return BoldRegex.Replace(escaped, "<strong>$1</strong>");
		}
	}
}
