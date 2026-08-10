using System.Globalization;
using System.Text;
using QRCoder;

namespace Diamond.Controls.Rendering
{
	/// <summary>
	/// Genera un QR en SVG (módulos vectoriales) para el sello de circulación.
	/// Contenido: <c>ZAFSEL:v1:{seal}:{payload}</c> verificable offline o en UI.
	/// </summary>
	public static class CirculationSheetQr
	{
		public const string QrPrefix = "ZAFSEL:v1:";

		/// <summary>Texto embebido en el QR.</summary>
		public static string BuildQrPayload(string sealCode, string authenticityPayload)
		{
			string seal = (sealCode ?? string.Empty).Trim();
			string pay = (authenticityPayload ?? string.Empty).Trim();
			// Compactar espacios en payload para QR más denso.
			pay = pay.Replace(" ", string.Empty);
			return QrPrefix + seal + ":" + pay;
		}

		/// <summary>
		/// Intenta parsear un QR o texto pegado.
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
				t = t.Substring(QrPrefix.Length);
				int colon = t.IndexOf(':');
				if (colon <= 0 || colon >= t.Length - 1)
				{
					return false;
				}

				sealCode = t.Substring(0, colon).Trim();
				authenticityPayload = t.Substring(colon + 1).Trim();
				return sealCode.Length > 0 && authenticityPayload.Length > 0;
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
			string qrText)
		{
			if (sb is null || string.IsNullOrEmpty(qrText) || sizePt < 8)
			{
				return;
			}

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
			// Fondo blanco para contraste en impresión.
			sb.Append(CultureInfo.InvariantCulture,
				$"<rect x=\"{F(x)}\" y=\"{F(y)}\" width=\"{F(sizePt)}\" height=\"{F(sizePt)}\" fill=\"#fff\" stroke=\"#000\" stroke-width=\"0.4\"/>");

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
							$"<rect x=\"{F(rx)}\" y=\"{F(ry)}\" width=\"{F(module + 0.02)}\" height=\"{F(module + 0.02)}\" fill=\"#000\"/>");
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
