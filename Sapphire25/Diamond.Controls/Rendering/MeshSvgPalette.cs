using System;
using System.Globalization;

namespace Diamond.Controls.Rendering
{
	/// <summary>
	/// Paleta del diagrama de malla: pantalla (oscuro) o papel (claro, ahorro de tóner).
	/// </summary>
	public readonly struct MeshSvgPalette
	{
		private readonly bool mvarIsPaper;

		private MeshSvgPalette(bool isPaper)
		{
			mvarIsPaper = isPaper;
		}

		public static MeshSvgPalette Screen
		{
			get { return new MeshSvgPalette(false); }
		}

		public static MeshSvgPalette Paper
		{
			get { return new MeshSvgPalette(true); }
		}

		public bool IsPaper
		{
			get { return mvarIsPaper; }
		}

		public string Background
		{
			get { return mvarIsPaper ? "#ffffff" : "#0f1419"; }
		}

		public string PlotBackground
		{
			get { return mvarIsPaper ? "#fafafa" : "#1a2332"; }
		}

		public string PlotBorder
		{
			get { return mvarIsPaper ? "#666666" : "#3d4f66"; }
		}

		public string GridMajor
		{
			get { return mvarIsPaper ? "#c8c8c8" : "#334155"; }
		}

		public string GridMinor
		{
			get { return mvarIsPaper ? "#e4e4e4" : "#243041"; }
		}

		public string StationLinePrincipal
		{
			get { return mvarIsPaper ? "#999999" : "#475569"; }
		}

		public string StationLineHalt
		{
			get { return mvarIsPaper ? "#cccccc" : "#2a3544"; }
		}

		public string TextPrimary
		{
			get { return mvarIsPaper ? "#111111" : "#e2e8f0"; }
		}

		public string TextSecondary
		{
			get { return mvarIsPaper ? "#333333" : "#cbd5e1"; }
		}

		public string TextMuted
		{
			get { return mvarIsPaper ? "#555555" : "#94a3b8"; }
		}

		public string TextClock
		{
			get { return mvarIsPaper ? "#222222" : "#9fb3c8"; }
		}

		public string AxisTick
		{
			get { return mvarIsPaper ? "#666666" : "#94a3b8"; }
		}

		public string StripBorder
		{
			get { return mvarIsPaper ? "#888888" : "#64748b"; }
		}

		public string LabelHalo
		{
			get { return mvarIsPaper ? "#ffffff" : "#0f1419"; }
		}

		public string SelectionHalo
		{
			get { return mvarIsPaper ? "#333333" : "#fef08a"; }
		}

		public string SelectionLabel
		{
			get { return mvarIsPaper ? "#000000" : "#fef08a"; }
		}

		public string ConflictFill
		{
			get { return mvarIsPaper ? "#cc0000" : "#ef4444"; }
		}

		public string ConflictStroke
		{
			get { return mvarIsPaper ? "#880000" : "#fecaca"; }
		}

		public string OccupationFill
		{
			get { return mvarIsPaper ? "#666666" : "#38bdf8"; }
		}

		public string DefaultTrain
		{
			get { return mvarIsPaper ? "#333333" : "#94a3b8"; }
		}

		public string LegendBoxFill
		{
			get { return mvarIsPaper ? "#ffffff" : "#0f1419"; }
		}

		public string LegendBoxStroke
		{
			get { return mvarIsPaper ? "#888888" : "#475569"; }
		}

		public string BandStroke
		{
			get { return mvarIsPaper ? "#cccccc" : "#0f1419"; }
		}

		/// <summary>
		/// Color de traza de tren para el tema actual.
		/// En papel: conserva el matiz; colores claros de pantalla → tinta oscura;
		/// colores ya oscuros → tinta más clara (legible sin negro pleno).
		/// </summary>
		public string MapTrainColor(string? screenHex)
		{
			if (string.IsNullOrWhiteSpace(screenHex))
			{
				return DefaultTrain;
			}

			if (!mvarIsPaper)
			{
				return screenHex.Trim();
			}

			return MeshPrintColor.ToPaperInk(screenHex.Trim());
		}

		public string MapUiColor(string screenHex)
		{
			if (!mvarIsPaper || string.IsNullOrWhiteSpace(screenHex))
			{
				return screenHex ?? DefaultTrain;
			}

			return MeshPrintColor.ToPaperInk(screenHex.Trim());
		}
	}

	/// <summary>
	/// Transformación de color pantalla → tinta de papel (mismo matiz, contraste sobre blanco).
	/// </summary>
	public static class MeshPrintColor
	{
		/// <summary>
		/// Convierte un color de pantalla (#rgb / #rrggbb) a tinta de impresión:
		/// brillos de UI oscura → oscuros; tonos ya oscuros → más claros (no negro 100 %).
		/// </summary>
		public static string ToPaperInk(string hex)
		{
			byte r, g, b;
			if (!TryParseHex(hex, out r, out g, out b))
			{
				return "#333333";
			}

			RgbToHsl(r, g, b, out double h, out double s, out double l);

			// Luminancia relativa (sRGB) para decidir el mapa.
			double linR = SrgbToLinear(r / 255.0);
			double linG = SrgbToLinear(g / 255.0);
			double linB = SrgbToLinear(b / 255.0);
			double y = 0.2126 * linR + 0.7152 * linG + 0.0722 * linB;

			double outL;
			double outS = s;
			if (y >= 0.35)
			{
				// Neón / claro en pantalla oscura → tinta oscura del mismo matiz.
				outL = 0.22 + (1.0 - l) * 0.08;
				if (outL > 0.34)
				{
					outL = 0.34;
				}

				if (outL < 0.16)
				{
					outL = 0.16;
				}

				outS = Math.Min(1.0, s * 1.05);
			}
			else
			{
				// Ya oscuro en pantalla → en papel un tono más claro (gris tintado, no negro).
				outL = 0.42 + (0.35 - y) * 0.25;
				if (outL > 0.58)
				{
					outL = 0.58;
				}

				if (outL < 0.36)
				{
					outL = 0.36;
				}

				outS = Math.Min(0.75, s * 0.9);
			}

			HslToRgb(h, outS, outL, out r, out g, out b);
			return "#"
				+ r.ToString("x2", CultureInfo.InvariantCulture)
				+ g.ToString("x2", CultureInfo.InvariantCulture)
				+ b.ToString("x2", CultureInfo.InvariantCulture);
		}

		public static bool TryParseHex(string hex, out byte r, out byte g, out byte b)
		{
			r = 0;
			g = 0;
			b = 0;
			if (string.IsNullOrWhiteSpace(hex))
			{
				return false;
			}

			string t = hex.Trim();
			if (t.StartsWith("#", StringComparison.Ordinal))
			{
				t = t.Substring(1);
			}

			if (t.Length == 3)
			{
				r = ParseNibble(t[0]);
				g = ParseNibble(t[1]);
				b = ParseNibble(t[2]);
				r = (byte)(r * 17);
				g = (byte)(g * 17);
				b = (byte)(b * 17);
				return true;
			}

			if (t.Length == 6)
			{
				return byte.TryParse(t.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out r)
					&& byte.TryParse(t.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out g)
					&& byte.TryParse(t.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b);
			}

			return false;
		}

		private static byte ParseNibble(char c)
		{
			if (c >= '0' && c <= '9')
			{
				return (byte)(c - '0');
			}

			if (c >= 'a' && c <= 'f')
			{
				return (byte)(c - 'a' + 10);
			}

			if (c >= 'A' && c <= 'F')
			{
				return (byte)(c - 'A' + 10);
			}

			return 0;
		}

		private static double SrgbToLinear(double c)
		{
			if (c <= 0.04045)
			{
				return c / 12.92;
			}

			return Math.Pow((c + 0.055) / 1.055, 2.4);
		}

		private static void RgbToHsl(byte r, byte g, byte b, out double h, out double s, out double l)
		{
			double rd = r / 255.0;
			double gd = g / 255.0;
			double bd = b / 255.0;
			double max = Math.Max(rd, Math.Max(gd, bd));
			double min = Math.Min(rd, Math.Min(gd, bd));
			l = (max + min) * 0.5;
			if (Math.Abs(max - min) < 1e-9)
			{
				h = 0;
				s = 0;
				return;
			}

			double d = max - min;
			s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);
			if (max == rd)
			{
				h = (gd - bd) / d + (gd < bd ? 6.0 : 0.0);
			}
			else if (max == gd)
			{
				h = (bd - rd) / d + 2.0;
			}
			else
			{
				h = (rd - gd) / d + 4.0;
			}

			h /= 6.0;
		}

		private static void HslToRgb(double h, double s, double l, out byte r, out byte g, out byte b)
		{
			double rd, gd, bd;
			if (s < 1e-9)
			{
				rd = gd = bd = l;
			}
			else
			{
				double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
				double p = 2 * l - q;
				rd = HueToRgb(p, q, h + 1.0 / 3.0);
				gd = HueToRgb(p, q, h);
				bd = HueToRgb(p, q, h - 1.0 / 3.0);
			}

			r = (byte)Math.Clamp((int)Math.Round(rd * 255.0), 0, 255);
			g = (byte)Math.Clamp((int)Math.Round(gd * 255.0), 0, 255);
			b = (byte)Math.Clamp((int)Math.Round(bd * 255.0), 0, 255);
		}

		private static double HueToRgb(double p, double q, double t)
		{
			if (t < 0)
			{
				t += 1;
			}

			if (t > 1)
			{
				t -= 1;
			}

			if (t < 1.0 / 6.0)
			{
				return p + (q - p) * 6.0 * t;
			}

			if (t < 0.5)
			{
				return q;
			}

			if (t < 2.0 / 3.0)
			{
				return p + (q - p) * (2.0 / 3.0 - t) * 6.0;
			}

			return p;
		}
	}
}
