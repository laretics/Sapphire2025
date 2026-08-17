using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using Diamond.Topo;

namespace Sapphire2025.Pages.Engineer
{
	internal static class TopoStorageMapBuilder
	{
		private static readonly string[] Palette =
		{
			"#38bdf8", "#a78bfa", "#34d399", "#fbbf24", "#f472b6",
			"#22d3ee", "#fb7185", "#4ade80", "#c084fc", "#facc15"
		};

		public sealed class LegendItem
		{
			public LegendItem(string label, string color)
			{
				Label = label;
				Color = color;
			}

			public string Label { get; }

			public string Color { get; }
		}

		public static string Build(TopoLayout layout, List<LegendItem> legend)
		{
			legend.Clear();
			const double width = 960;
			const double height = 560;
			const double pad = 28;

			List<(double Lat, double Lon)> samples = new List<(double, double)>();
			int ai = 0;
			while (ai < layout.Axes.Count)
			{
				Axis axis = layout.Axes[ai];
				int vi = 0;
				while (vi < axis.Vertices.Count)
				{
					AxisVertex v = axis.Vertices[vi];
					if (IsFinite(v.Latitude) && IsFinite(v.Longitude))
					{
						samples.Add((v.Latitude, v.Longitude));
					}

					vi++;
				}

				ai++;
			}

			if (samples.Count < 2)
			{
				return string.Empty;
			}

			double minLat = samples[0].Lat;
			double maxLat = samples[0].Lat;
			double minLon = samples[0].Lon;
			double maxLon = samples[0].Lon;
			int si = 1;
			while (si < samples.Count)
			{
				(double lat, double lon) = samples[si];
				if (lat < minLat) minLat = lat;
				if (lat > maxLat) maxLat = lat;
				if (lon < minLon) minLon = lon;
				if (lon > maxLon) maxLon = lon;
				si++;
			}

			double dLat = maxLat - minLat;
			double dLon = maxLon - minLon;
			if (dLat < 1e-8) dLat = 0.01;
			if (dLon < 1e-8) dLon = 0.01;
			double midLat = (minLat + maxLat) * 0.5;
			double metersPerDegLon = 111320.0 * Math.Cos(midLat * Math.PI / 180.0);
			if (metersPerDegLon < 1.0) metersPerDegLon = 1.0;
			double metersPerDegLat = 111320.0;
			double spanX = dLon * metersPerDegLon;
			double spanY = dLat * metersPerDegLat;
			double drawW = width - 2 * pad;
			double drawH = height - 2 * pad;
			double scale = Math.Min(drawW / spanX, drawH / spanY);
			double usedW = spanX * scale;
			double usedH = spanY * scale;
			double ox = pad + (drawW - usedW) * 0.5;
			double oy = pad + (drawH - usedH) * 0.5;

			StringBuilder sb = new StringBuilder(8192);
			sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 ");
			sb.Append(width.ToString(CultureInfo.InvariantCulture));
			sb.Append(' ');
			sb.Append(height.ToString(CultureInfo.InvariantCulture));
			sb.Append("\" preserveAspectRatio=\"xMidYMid meet\" role=\"img\" aria-label=\"Mapa de topología\">");
			sb.Append("<defs><pattern id=\"topoGrid\" width=\"24\" height=\"24\" patternUnits=\"userSpaceOnUse\">");
			sb.Append("<path d=\"M24 0H0V24\" fill=\"none\" stroke=\"rgba(148,163,184,0.12)\" stroke-width=\"1\"/></pattern>");
			sb.Append("<filter id=\"glow\" x=\"-20%\" y=\"-20%\" width=\"140%\" height=\"140%\">");
			sb.Append("<feGaussianBlur stdDeviation=\"1.2\" result=\"b\"/><feMerge><feMergeNode in=\"b\"/><feMergeNode in=\"SourceGraphic\"/></feMerge></filter></defs>");
			sb.Append("<rect width=\"100%\" height=\"100%\" fill=\"#0b1220\"/><rect width=\"100%\" height=\"100%\" fill=\"url(#topoGrid)\"/>");

			int axisIndex = 0;
			while (axisIndex < layout.Axes.Count)
			{
				Axis axis = layout.Axes[axisIndex];
				string color = ResolveAxisColor(axis, axisIndex);
				string label = string.IsNullOrWhiteSpace(axis.Name)
					? (string.IsNullOrWhiteSpace(axis.Id) ? "Eje " + (axisIndex + 1).ToString() : axis.Id)
					: axis.Name;
				legend.Add(new LegendItem(label, color));

				StringBuilder poly = new StringBuilder();
				int first = 1;
				int v = 0;
				while (v < axis.Vertices.Count)
				{
					AxisVertex vertex = axis.Vertices[v];
					if (IsFinite(vertex.Latitude) && IsFinite(vertex.Longitude))
					{
						double x = ox + (vertex.Longitude - minLon) * metersPerDegLon * scale;
						double y = oy + (maxLat - vertex.Latitude) * metersPerDegLat * scale;
						if (first == 1)
						{
							poly.Append(x.ToString("0.###", CultureInfo.InvariantCulture));
							poly.Append(',');
							poly.Append(y.ToString("0.###", CultureInfo.InvariantCulture));
							first = 0;
						}
						else
						{
							poly.Append(' ');
							poly.Append(x.ToString("0.###", CultureInfo.InvariantCulture));
							poly.Append(',');
							poly.Append(y.ToString("0.###", CultureInfo.InvariantCulture));
						}
					}

					v++;
				}

				if (poly.Length > 0)
				{
					sb.Append("<polyline fill=\"none\" stroke=\"");
					sb.Append(HtmlEncoder.Default.Encode(color));
					sb.Append("\" stroke-width=\"2.6\" stroke-linecap=\"round\" stroke-linejoin=\"round\" filter=\"url(#glow)\" points=\"");
					sb.Append(poly);
					sb.Append("\"/>");
				}

				axisIndex++;
			}

			HashSet<string> drawn = new HashSet<string>(StringComparer.Ordinal);
			int st = 0;
			while (st < layout.Stations.Count)
			{
				Station station = layout.Stations[st];
				if (!station.Latitude.HasValue || !station.Longitude.HasValue)
				{
					st++;
					continue;
				}

				double lat = station.Latitude.Value;
				double lon = station.Longitude.Value;
				if (!IsFinite(lat) || !IsFinite(lon))
				{
					st++;
					continue;
				}

				string key = station.Id.Length > 0
					? station.Id
					: lat.ToString("F5", CultureInfo.InvariantCulture) + "," + lon.ToString("F5", CultureInfo.InvariantCulture);
				if (!drawn.Add(key))
				{
					st++;
					continue;
				}

				double sx = ox + (lon - minLon) * metersPerDegLon * scale;
				double sy = oy + (maxLat - lat) * metersPerDegLat * scale;
				sb.Append("<circle cx=\"");
				sb.Append(sx.ToString("0.###", CultureInfo.InvariantCulture));
				sb.Append("\" cy=\"");
				sb.Append(sy.ToString("0.###", CultureInfo.InvariantCulture));
				sb.Append("\" r=\"3.8\" fill=\"#f8fafc\" stroke=\"#0ea5e9\" stroke-width=\"1.4\"/>");
				string text = !string.IsNullOrWhiteSpace(station.Avr)
					? station.Avr
					: (!string.IsNullOrWhiteSpace(station.Name) ? station.Name : station.Id);
				if (!string.IsNullOrWhiteSpace(text))
				{
					sb.Append("<text x=\"");
					sb.Append((sx + 6).ToString("0.###", CultureInfo.InvariantCulture));
					sb.Append("\" y=\"");
					sb.Append((sy - 6).ToString("0.###", CultureInfo.InvariantCulture));
					sb.Append("\" fill=\"#e2e8f0\" font-size=\"10\" font-family=\"Segoe UI, system-ui, sans-serif\">");
					sb.Append(HtmlEncoder.Default.Encode(text));
					sb.Append("</text>");
				}

				st++;
			}

			sb.Append("</svg>");
			return sb.ToString();
		}

		private static string ResolveAxisColor(Axis axis, int index)
		{
			if (!string.IsNullOrWhiteSpace(axis.Color) && LooksLikeCssColor(axis.Color))
			{
				return axis.Color.Trim();
			}

			if (!string.IsNullOrWhiteSpace(axis.DarkColor) && LooksLikeCssColor(axis.DarkColor))
			{
				return axis.DarkColor.Trim();
			}

			return Palette[index % Palette.Length];
		}

		private static bool LooksLikeCssColor(string value)
		{
			string v = value.Trim();
			return (v.StartsWith('#') && (v.Length == 4 || v.Length == 7 || v.Length == 9))
				|| v.StartsWith("rgb", StringComparison.OrdinalIgnoreCase);
		}

		private static bool IsFinite(double value)
		{
			return !double.IsNaN(value) && !double.IsInfinity(value);
		}
	}
}
