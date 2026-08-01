using System;
using System.Collections.Generic;
using Diamond.Topo;

namespace Diamond.Timed
{
	/// <summary>
	/// Resuelve y aplica <see cref="DemandTopoConstraint"/> sobre la topología en memoria
	/// como capas de sesión (<see cref="Axis.SessionLimits"/> / <see cref="Axis.SessionTrackSpans"/>),
	/// sin alterar límites fijos ni tramos de vías del XML base.
	/// </summary>
	public static class DemandTopoOverlay
	{
		/// <summary>
		/// Limpia todas las capas de sesión de todos los ejes del layout.
		/// </summary>
		public static void Clear(TopoLayout layout)
		{
			if (layout is null)
			{
				throw new ArgumentNullException(nameof(layout));
			}

			int i = 0;
			while (i < layout.Axes.Count)
			{
				layout.Axes[i].ClearSessionOverlays();
				i++;
			}
		}

		/// <summary>
		/// Resuelve estaciones/PK y aplica las restricciones. Limpia primero las capas de sesión.
		/// Errores de resolución se añaden a <paramref name="errors"/> (no lanza).
		/// </summary>
		public static void Apply(
			TopoLayout layout,
			IReadOnlyList<DemandTopoConstraint> constraints,
			List<string>? errors = null)
		{
			if (layout is null)
			{
				throw new ArgumentNullException(nameof(layout));
			}

			List<string> errorList = errors ?? new List<string>();
			Clear(layout);

			if (constraints is null || constraints.Count == 0)
			{
				return;
			}

			int index = 0;
			while (index < constraints.Count)
			{
				DemandTopoConstraint c = constraints[index];
				string? error;
				if (!TryResolveAndApply(layout, c, out error))
				{
					errorList.Add(FormatError(c, error ?? "no se pudo aplicar la restricción."));
				}

				index++;
			}
		}

		/// <summary>
		/// Resuelve un par de estaciones a un tramo [pk0, pkf) en un eje y lo aplica.
		/// </summary>
		public static bool TryResolveAndApply(
			TopoLayout layout,
			DemandTopoConstraint constraint,
			out string? error)
		{
			error = null;
			if (constraint is null)
			{
				error = "restricción null.";
				return false;
			}

			// Estaciones (pueden venir ya resueltas por el resolver general).
			Station? from = constraint.FromStation;
			Station? to = constraint.ToStation;
			if (from is null)
			{
				string? resolveError;
				if (!DemandStationResolver.TryResolve(constraint.From.Text, layout, out from, out resolveError)
					|| from is null)
				{
					error = "origen: " + (resolveError ?? "estación desconocida.");
					return false;
				}

				constraint.FromStation = from;
			}

			if (to is null)
			{
				string? resolveError;
				if (!DemandStationResolver.TryResolve(constraint.To.Text, layout, out to, out resolveError)
					|| to is null)
				{
					error = "destino: " + (resolveError ?? "estación desconocida.");
					return false;
				}

				constraint.ToStation = to;
			}

			Axis? axis;
			long pk0;
			long pkf;
			if (!TryLocateSpan(layout, from, to, constraint.AxisId, out axis, out pk0, out pkf, out error)
				|| axis is null)
			{
				return false;
			}

			if (pkf <= pk0)
			{
				error = "tramo vacío entre " + constraint.From.Text + " y " + constraint.To.Text + ".";
				return false;
			}

			if (constraint.Kind == DemandTopoConstraintKind.TrackCount)
			{
				axis.SetSessionTrackCount(pk0, pkf, constraint.Value);
			}
			else
			{
				axis.SessionLimits.Add(constraint.Value, pk0, pkf);
			}

			constraint.MarkResolved(axis, pk0, pkf);
			return true;
		}

		/// <summary>
		/// Localiza un eje que contenga ambas estaciones y devuelve [minPk, maxPk+1).
		/// </summary>
		public static bool TryLocateSpan(
			TopoLayout layout,
			Station from,
			Station to,
			string? preferredAxisId,
			out Axis? axis,
			out long pk0,
			out long pkf,
			out string? error)
		{
			axis = null;
			pk0 = 0L;
			pkf = 0L;
			error = null;

			if (from is null || to is null)
			{
				error = "estaciones null.";
				return false;
			}

			if (ReferenceEquals(from, to) || string.Equals(from.Id, to.Id, StringComparison.Ordinal))
			{
				error = "origen y destino deben ser estaciones distintas.";
				return false;
			}

			List<AxisCandidate> candidates = new List<AxisCandidate>();
			int ai = 0;
			while (ai < layout.Axes.Count)
			{
				Axis a = layout.Axes[ai];
				if (!string.IsNullOrWhiteSpace(preferredAxisId)
					&& !string.Equals(a.Id, preferredAxisId.Trim(), StringComparison.OrdinalIgnoreCase))
				{
					ai++;
					continue;
				}

				StationOnAxis? pFrom = FindPlacement(a, from);
				StationOnAxis? pTo = FindPlacement(a, to);
				if (pFrom is not null && pTo is not null)
				{
					long lo = pFrom.PK < pTo.PK ? pFrom.PK : pTo.PK;
					long hi = pFrom.PK < pTo.PK ? pTo.PK : pFrom.PK;
					// Semiabierto inclusivo en la práctica: +1 m en el extremo (como doble vía demo).
					long endExclusive = hi + 1L;
					if (a.IsBuilt && endExclusive > a.PKEnd)
					{
						endExclusive = a.PKEnd;
						if (endExclusive <= lo && a.PKEnd > lo)
						{
							endExclusive = a.PKEnd;
						}
					}

					candidates.Add(new AxisCandidate(a, lo, endExclusive));
				}

				ai++;
			}

			if (candidates.Count == 0)
			{
				if (!string.IsNullOrWhiteSpace(preferredAxisId))
				{
					error = "no hay tramo " + StationLabel(from) + "–" + StationLabel(to)
						+ " en el eje '" + preferredAxisId.Trim() + "'.";
				}
				else
				{
					error = "no hay un eje común con " + StationLabel(from) + " y " + StationLabel(to) + ".";
				}

				return false;
			}

			// Determinista: eje con Id menor; si empate, tramo más corto.
			candidates.Sort(static (x, y) =>
			{
				int byId = string.CompareOrdinal(x.Axis.Id, y.Axis.Id);
				if (byId != 0)
				{
					return byId;
				}

				long lenX = x.Pkf - x.Pk0;
				long lenY = y.Pkf - y.Pk0;
				return lenX.CompareTo(lenY);
			});

			AxisCandidate chosen = candidates[0];
			if (candidates.Count > 1 && string.IsNullOrWhiteSpace(preferredAxisId))
			{
				// Aviso no fatal: elegimos el primero determinista; el script puede fijar "on EJE".
			}

			axis = chosen.Axis;
			pk0 = chosen.Pk0;
			pkf = chosen.Pkf;
			return true;
		}

		private static StationOnAxis? FindPlacement(Axis axis, Station station)
		{
			int i = 0;
			while (i < axis.Stations.Count)
			{
				StationOnAxis p = axis.Stations[i];
				if (ReferenceEquals(p.Station, station)
					|| string.Equals(p.Station.Id, station.Id, StringComparison.Ordinal))
				{
					return p;
				}

				// Misma identidad operativa (AVR) en layouts legacy duplicados.
				if (station.Avr.Length > 0
					&& string.Equals(p.Station.Avr, station.Avr, StringComparison.OrdinalIgnoreCase))
				{
					return p;
				}

				i++;
			}

			return null;
		}

		private static string FormatError(DemandTopoConstraint c, string detail)
		{
			string prefix = c.SourceLine > 0
				? "line " + c.SourceLine.ToString(System.Globalization.CultureInfo.InvariantCulture) + ": "
				: string.Empty;
			return prefix + c.ToString() + ": " + detail;
		}

		private static string StationLabel(Station station)
		{
			if (station is null)
			{
				return "?";
			}

			if (!string.IsNullOrEmpty(station.Avr))
			{
				return station.Avr;
			}

			return station.Id;
		}

		private readonly struct AxisCandidate
		{
			public AxisCandidate(Axis axis, long pk0, long pkf)
			{
				Axis = axis;
				Pk0 = pk0;
				Pkf = pkf;
			}

			public Axis Axis { get; }
			public long Pk0 { get; }
			public long Pkf { get; }
		}
	}
}
