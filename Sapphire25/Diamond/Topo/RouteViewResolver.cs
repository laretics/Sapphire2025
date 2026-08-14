using System;
using System.Collections.Generic;
using System.Globalization;

namespace Diamond.Topo
{
	/// <summary>
	/// Resuelve un <see cref="RouteView"/> a partir de un ViewId de asimilación
	/// (p. ej. "T3", "T3+T2" o la firma "T3:0&gt;32000+T2:5000&gt;18000")
	/// o del eje más cercano.
	/// </summary>
	public static class RouteViewResolver
	{
		/// <summary>
		/// Interpreta ViewId / PathSignature.
		/// Acepta un eje ("T3"), tramos con PK ("T3:0&gt;32000+T2:5&gt;18") y, si no hay
		/// rangos, no concatena ejes enteros (eso no es T3+T2 Palma–SPB).
		/// </summary>
		public static RouteView? TryFromViewId(TopoLayout topo, string? viewId)
		{
			if (topo is null || string.IsNullOrWhiteSpace(viewId))
			{
				return null;
			}

			string[] parts = viewId.Split(
				new[] { '+', '|', ',', ';' },
				StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			if (parts.Length == 0)
			{
				return null;
			}

			List<(Axis Axis, long FromPk, long ToPk)> ranged =
				new List<(Axis, long, long)>();
			int rangedCount = 0;
			int bareCount = 0;
			int i = 0;
			while (i < parts.Length)
			{
				string part = parts[i];
				i++;
				string axisId;
				long fromPk;
				long toPk;
				bool hasRange = TryParseRangedPart(part, out axisId, out fromPk, out toPk);
				if (!hasRange)
				{
					axisId = part;
				}

				Axis? axis = topo.FindAxisById(axisId);
				if (axis is null)
				{
					return null;
				}

				if (hasRange)
				{
					if (fromPk == toPk)
					{
						return null;
					}

					ranged.Add((axis, fromPk, toPk));
					rangedCount++;
				}
				else
				{
					bareCount++;
					if (parts.Length == 1)
					{
						try
						{
							return RouteView.FromAxis(axis);
						}
						catch (InvalidOperationException)
						{
							return null;
						}
					}
				}
			}

			// "T3+T2" sin PK: no encadenar ejes completos (Palma–Manacor + T2).
			if (bareCount > 0)
			{
				return null;
			}

			if (rangedCount == 0)
			{
				return null;
			}

			try
			{
				return RouteView.Concat(viewId.Trim(), viewId.Trim(), ranged);
			}
			catch (ArgumentException)
			{
				return null;
			}
			catch (InvalidOperationException)
			{
				return null;
			}
		}

		/// <summary>
		/// Resuelve la vista de una circulación de cabina: firma de camino, ViewId, o camino OD.
		/// </summary>
		public static RouteView? TryForCabinCirculation(
			TopoLayout topo,
			string? viewId,
			string? pathSignature,
			string? originStationId,
			string? destinationStationId,
			string? originAvr,
			string? destinationAvr)
		{
			if (topo is null)
			{
				return null;
			}

			if (!string.IsNullOrWhiteSpace(pathSignature))
			{
				RouteView? fromSig = TryFromViewId(topo, pathSignature);
				if (fromSig is not null)
				{
					return fromSig;
				}
			}

			if (!string.IsNullOrWhiteSpace(viewId))
			{
				RouteView? fromId = TryFromViewId(topo, viewId);
				if (fromId is not null)
				{
					return fromId;
				}
			}

			Station? from = FindStation(topo, originStationId, originAvr);
			Station? to = FindStation(topo, destinationStationId, destinationAvr);
			if (from is null || to is null)
			{
				return null;
			}

			RouteView? path;
			if (RouteView.TryFindPath(topo, from, to, out path, out _, out _) && path is not null)
			{
				return path;
			}

			return null;
		}

		/// <summary>Parte "T3:1200&gt;44800" → eje + PKs de eje.</summary>
		public static bool TryParseRangedPart(string part, out string axisId, out long fromPk, out long toPk)
		{
			axisId = string.Empty;
			fromPk = 0;
			toPk = 0;
			if (string.IsNullOrWhiteSpace(part))
			{
				return false;
			}

			int colon = part.IndexOf(':');
			if (colon <= 0 || colon >= part.Length - 1)
			{
				return false;
			}

			int gt = part.IndexOf('>', colon + 1);
			if (gt <= colon + 1 || gt >= part.Length - 1)
			{
				return false;
			}

			axisId = part.Substring(0, colon).Trim();
			if (axisId.Length == 0)
			{
				return false;
			}

			string fromText = part.Substring(colon + 1, gt - colon - 1).Trim();
			string toText = part.Substring(gt + 1).Trim();
			if (!long.TryParse(fromText, NumberStyles.Integer, CultureInfo.InvariantCulture, out fromPk))
			{
				return false;
			}

			if (!long.TryParse(toText, NumberStyles.Integer, CultureInfo.InvariantCulture, out toPk))
			{
				return false;
			}

			return true;
		}

		/// <summary>Eje de un trozo de ViewId/firma ("T3" o "T3:0&gt;1" → "T3").</summary>
		public static string AxisIdFromPart(string part)
		{
			if (string.IsNullOrWhiteSpace(part))
			{
				return string.Empty;
			}

			int colon = part.IndexOf(':');
			if (colon <= 0)
			{
				return part.Trim();
			}

			return part.Substring(0, colon).Trim();
		}

		private static Station? FindStation(TopoLayout topo, string? id, string? avr)
		{
			if (!string.IsNullOrWhiteSpace(id))
			{
				Station? byId = topo.FindStationById(id);
				if (byId is not null)
				{
					return byId;
				}
			}

			if (string.IsNullOrWhiteSpace(avr))
			{
				return null;
			}

			int i = 0;
			while (i < topo.Stations.Count)
			{
				Station st = topo.Stations[i];
				if (string.Equals(st.Avr, avr, StringComparison.OrdinalIgnoreCase))
				{
					return st;
				}

				i++;
			}

			return null;
		}

		/// <summary>
		/// Vista de un eje concreto, o null.
		/// </summary>
		public static RouteView? TryFromAxis(Axis? axis)
		{
			if (axis is null)
			{
				return null;
			}

			try
			{
				return RouteView.FromAxis(axis);
			}
			catch (InvalidOperationException)
			{
				return null;
			}
		}

		/// <summary>
		/// Primer eje usable del layout.
		/// </summary>
		public static RouteView? TryFirstAxis(TopoLayout? topo)
		{
			if (topo is null || topo.Axes.Count == 0)
			{
				return null;
			}

			int i = 0;
			while (i < topo.Axes.Count)
			{
				RouteView? view = TryFromAxis(topo.Axes[i]);
				if (view is not null)
				{
					return view;
				}

				i++;
			}

			return null;
		}
	}
}
