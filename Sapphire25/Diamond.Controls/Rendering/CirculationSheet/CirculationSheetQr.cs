using System.Globalization;
using System.Text;
using QRCoder;

namespace Diamond.Controls.Rendering
{
	/// <summary>
	/// Genera un QR en SVG (módulos vectoriales) para el sello de circulación.
	/// Contenido: <c>ZAFSEL:v1:{seal}</c> (solo sello; el payload canónico vive en el registro / UI).
	/// </summary>
	public static class CirculationSheetQr
	{
		public const string QrPrefix = "ZAFSEL:v1:";

		/// <summary>
		/// Texto embebido en el QR: prefijo + sello de 12 hex.
		/// <paramref name="authenticityPayload"/> se ignora (compat. con llamadas antiguas).
		/// </summary>
		public static string BuildQrPayload(string sealCode, string? authenticityPayload = null)
		{
			string seal = (sealCode ?? string.Empty).Trim();
			if (seal.StartsWith(CirculationSheetAuthenticity.SealPrefix, StringComparison.OrdinalIgnoreCase))
			{
				seal = seal.Substring(CirculationSheetAuthenticity.SealPrefix.Length).Trim();
			}

			return QrPrefix + seal;
		}

		/// <summary>
		/// Intenta parsear un QR o texto pegado.
		/// Acepta <c>ZAFSEL:v1:{seal}</c>, legado <c>ZAFSEL:v1:{seal}:{payload}</c>,
		/// <c>SEL …</c> o hex de 12.
		/// </summary>
		public static bool TryParseQrPayload(string? text, out string sealCode, out string authenticityPayload)
		{
			sealCode = string.Empty;
			authenticityPayload = string.Empty;
			if (string.IsNullOrWhiteSpace(text))
			{
				return false;
			}

			string t = text.Trim();
			if (t.StartsWith(QrPrefix, StringComparison.OrdinalIgnoreCase))
			{
				t = t.Substring(QrPrefix.Length).Trim();
				if (t.Length == 0)
				{
					return false;
				}

				// Legado: seal:payload → solo sello; payload opcional a la derecha.
				int colon = t.IndexOf(':');
				if (colon > 0)
				{
					sealCode = t.Substring(0, colon).Trim();
					if (colon < t.Length - 1)
					{
						authenticityPayload = t.Substring(colon + 1).Trim();
					}
				}
				else
				{
					sealCode = t;
				}

				return sealCode.Length > 0;
			}

			// Solo sello (sin payload): la UI pedirá reconstruir o buscar en registro.
			if (t.StartsWith(CirculationSheetAuthenticity.SealPrefix, StringComparison.OrdinalIgnoreCase))
			{
				sealCode = t.Substring(CirculationSheetAuthenticity.SealPrefix.Length).Trim();
				return sealCode.Length > 0;
			}

			// Hex sello suelto
			if (t.Length == 12 && IsHex(t))
			{
				sealCode = t.ToLowerInvariant();
				return true;
			}

			return false;
		}

		/// <summary>
		/// Dibuja un QR SVG (grupo) en la esquina inferior derecha del panel.
		/// </summary>
		public static void AppendQrSvg(
			StringBuilder sb,
			double x,
			double y,
			double sizePt,
			string qrText,
			string? moduleFill = null,
			string? paperFill = null,
			string? frameStroke = null)
		{
			if (sb is null || string.IsNullOrEmpty(qrText) || sizePt < 8)
			{
				return;
			}

			string dark = string.IsNullOrEmpty(moduleFill) ? "#000" : moduleFill;
			string paper = string.IsNullOrEmpty(paperFill) ? "#fff" : paperFill;
			string frame = string.IsNullOrEmpty(frameStroke) ? dark : frameStroke;

			using QRCodeGenerator gen = new QRCodeGenerator();
			using QRCodeData data = gen.CreateQrCode(qrText, QRCodeGenerator.ECCLevel.Q);
			int modules = data.ModuleMatrix.Count;
			if (modules < 1)
			{
				return;
			}

			double module = sizePt / modules;
			sb.Append(CultureInfo.InvariantCulture,
				$"<g class=\"diamond-circ-qr\" opacity=\"0.92\">");
			sb.Append(CultureInfo.InvariantCulture,
				$"<rect x=\"{F(x)}\" y=\"{F(y)}\" width=\"{F(sizePt)}\" height=\"{F(sizePt)}\" fill=\"{paper}\" stroke=\"{frame}\" stroke-width=\"0.4\"/>");

			int r = 0;
			while (r < modules)
			{
				int c = 0;
				while (c < modules)
				{
					if (data.ModuleMatrix[r][c])
					{
						double rx = x + c * module;
						double ry = y + r * module;
						sb.Append(CultureInfo.InvariantCulture,
							$"<rect x=\"{F(rx)}\" y=\"{F(ry)}\" width=\"{F(module + 0.02)}\" height=\"{F(module + 0.02)}\" fill=\"{dark}\"/>");
					}

					c++;
				}

				r++;
			}

			sb.Append("</g>");
		}

		private static bool IsHex(string s)
		{
			int i = 0;
			while (i < s.Length)
			{
				char ch = s[i];
				bool ok = (ch >= '0' && ch <= '9')
					|| (ch >= 'a' && ch <= 'f')
					|| (ch >= 'A' && ch <= 'F');
				if (!ok)
				{
					return false;
				}

				i++;
			}

			return true;
		}

		private static string F(double v)
		{
			return v.ToString("0.###", CultureInfo.InvariantCulture);
		}
	}
}
