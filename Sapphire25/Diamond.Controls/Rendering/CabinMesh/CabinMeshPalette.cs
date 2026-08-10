using System;

namespace Diamond.Controls.Rendering.CabinMesh
{
	/// <summary>
	/// Paleta día / noche para la malla de cabina.
	/// </summary>
	public sealed class CabinMeshPalette
	{
		private CabinMeshPalette(
			string stationLinePrimary,
			string stationLineSecondary,
			string stationLabel,
			string timeLineHour,
			string timeLineMinute,
			string timeLabel,
			string nowLine,
			string trainFallback,
			string trainActiveGlow,
			double trainInactiveOpacity)
		{
			StationLinePrimary = stationLinePrimary;
			StationLineSecondary = stationLineSecondary;
			StationLabel = stationLabel;
			TimeLineHour = timeLineHour;
			TimeLineMinute = timeLineMinute;
			TimeLabel = timeLabel;
			NowLine = nowLine;
			TrainFallback = trainFallback;
			TrainActiveGlow = trainActiveGlow;
			TrainInactiveOpacity = trainInactiveOpacity;
		}

		public string StationLinePrimary { get; }

		public string StationLineSecondary { get; }

		public string StationLabel { get; }

		public string TimeLineHour { get; }

		public string TimeLineMinute { get; }

		public string TimeLabel { get; }

		public string NowLine { get; }

		public string TrainFallback { get; }

		public string TrainActiveGlow { get; }

		public double TrainInactiveOpacity { get; }

		public static CabinMeshPalette Day
		{
			get
			{
				return new CabinMeshPalette(
					stationLinePrimary: "#6a9fd8",
					stationLineSecondary: "#9ec3e8",
					stationLabel: "#4a7aad",
					timeLineHour: "#9a9a9a",
					timeLineMinute: "#c8c8c8",
					timeLabel: "#777777",
					nowLine: "#5b8fc7",
					trainFallback: "#2c5aa0",
					trainActiveGlow: "none",
					trainInactiveOpacity: 0.72);
			}
		}

		public static CabinMeshPalette Night
		{
			get
			{
				return new CabinMeshPalette(
					stationLinePrimary: "#b898a0",
					stationLineSecondary: "#8a7078",
					stationLabel: "#c8a8b0",
					timeLineHour: "#a04040",
					timeLineMinute: "#703030",
					timeLabel: "#c06060",
					nowLine: "#e07070",
					trainFallback: "#ffb070",
					trainActiveGlow: "0 0 4px rgba(255,200,120,0.85)",
					trainInactiveOpacity: 0.85);
			}
		}

		public static CabinMeshPalette ForNightMode(bool night)
		{
			return night ? Night : Day;
		}

		/// <summary>
		/// Color de tren con contraste razonable sobre fondo claro/oscuro.
		/// </summary>
		public string ResolveTrainColor(string? raw, bool night)
		{
			string c = (raw ?? string.Empty).Trim();
			if (c.Length == 0)
			{
				return TrainFallback;
			}

			if (!c.StartsWith("#", StringComparison.Ordinal) || c.Length < 7)
			{
				return c;
			}

			if (!TryParseRgb(c, out int r, out int g, out int b))
			{
				return c;
			}

			double lum = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0;
			if (night)
			{
				// Resaltar: subir luminancia mínima.
				if (lum < 0.45)
				{
					return Lighten(r, g, b, 0.45);
				}
			}
			else
			{
				// Oscurecer un poco si es casi blanco.
				if (lum > 0.82)
				{
					return Darken(r, g, b, 0.35);
				}
			}

			return c.Length == 7 ? c : "#" + c.Substring(1, 6);
		}

		private static bool TryParseRgb(string hex, out int r, out int g, out int b)
		{
			r = g = b = 0;
			string h = hex.StartsWith("#", StringComparison.Ordinal) ? hex.Substring(1) : hex;
			if (h.Length < 6)
			{
				return false;
			}

			return int.TryParse(h.AsSpan(0, 2), System.Globalization.NumberStyles.HexNumber, null, out r)
				&& int.TryParse(h.AsSpan(2, 2), System.Globalization.NumberStyles.HexNumber, null, out g)
				&& int.TryParse(h.AsSpan(4, 2), System.Globalization.NumberStyles.HexNumber, null, out b);
		}

		private static string Lighten(int r, int g, int b, double minLum)
		{
			double lum = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0;
			if (lum >= minLum)
			{
				return string.Format("#{0:X2}{1:X2}{2:X2}", r, g, b);
			}

			double t = (minLum - lum) / (1.0 - lum + 1e-6);
			int nr = (int)Math.Clamp(r + (255 - r) * t, 0, 255);
			int ng = (int)Math.Clamp(g + (255 - g) * t, 0, 255);
			int nb = (int)Math.Clamp(b + (255 - b) * t, 0, 255);
			return string.Format("#{0:X2}{1:X2}{2:X2}", nr, ng, nb);
		}

		private static string Darken(int r, int g, int b, double factor)
		{
			int nr = (int)Math.Clamp(r * (1.0 - factor), 0, 255);
			int ng = (int)Math.Clamp(g * (1.0 - factor), 0, 255);
			int nb = (int)Math.Clamp(b * (1.0 - factor), 0, 255);
			return string.Format("#{0:X2}{1:X2}{2:X2}", nr, ng, nb);
		}
	}
}
