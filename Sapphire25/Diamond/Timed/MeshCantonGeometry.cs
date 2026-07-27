using System;
using System.Collections.Generic;
using Diamond.Motion;
using Diamond.Topo;

namespace Diamond.Timed
{
	/// <summary>
	/// Helper de geometría de acantonamiento sobre una <see cref="Mesh"/>:
	/// cada ocupación de cantón por un tren es un rectángulo tiempo × espacio (PK de ruta).
	/// El tiempo de parada en estación principal (AVR en mayúsculas) no ocupa cantón de vía.
	/// </summary>
	public static class MeshCantonGeometry
	{
		/// <summary>
		/// Intervalo de ocupación de vía (sin dwell de estación).
		/// </summary>
		public readonly struct TrackOccupationInterval
		{
			private readonly TimeSpan mvarEnter;
			private readonly TimeSpan mvarExit;

			public TrackOccupationInterval(TimeSpan enter, TimeSpan exit)
			{
				mvarEnter = enter;
				mvarExit = exit;
			}

			public TimeSpan Enter
			{
				get { return mvarEnter; }
			}

			public TimeSpan Exit
			{
				get { return mvarExit; }
			}
		}

		/// <summary>
		/// Construye todos los rectángulos de ocupación de cantón de las circulaciones
		/// proyectables sobre <paramref name="view"/>.
		/// </summary>
		public static IReadOnlyList<CantonOccupationRect> BuildOccupations(Mesh mesh, RouteView view)
		{
			if (mesh is null)
			{
				throw new ArgumentNullException(nameof(mesh));
			}

			if (view is null)
			{
				throw new ArgumentNullException(nameof(view));
			}

			List<CantonOccupationRect> result = new List<CantonOccupationRect>();
			IReadOnlyList<long> frontiers = view.CantonFrontiers;
			if (frontiers.Count < 2)
			{
				// Sin fronteras: un solo “cantón” por trayecto, troceado por paradas en estación.
				int ci = 0;
				while (ci < mesh.Circulations.Count)
				{
					Circulation c = mesh.Circulations[ci];
					if (!IsVisibleOnView(c.Asimilation, view))
					{
						ci++;
						continue;
					}

					// Bounding box espacial en coords de la vista de dibujo.
					long pk0 = view.PK;
					long pk1 = view.PKEnd;
					AppendOccupationsForCanton(
						result,
						c.Id,
						view.Id,
						pk0,
						pk1,
						c.Departure,
						c.Asimilation,
						view);
					ci++;
				}

				return result;
			}

			int cIndex = 0;
			while (cIndex < mesh.Circulations.Count)
			{
				Circulation circulation = mesh.Circulations[cIndex];
				if (!IsVisibleOnView(circulation.Asimilation, view))
				{
					cIndex++;
					continue;
				}

				Asimilation asim = circulation.Asimilation;
				int f = 0;
				while (f < frontiers.Count - 1)
				{
					long pk0 = frontiers[f];
					long pkf = frontiers[f + 1];

					AppendOccupationsForCanton(
						result,
						circulation.Id,
						view.Id,
						pk0,
						pkf,
						circulation.Departure,
						asim,
						view);

					f++;
				}

				cIndex++;
			}

			return result;
		}

		/// <summary>
		/// Intervalos absolutos de ocupación de vía en el cantón [pk0, pkf)
		/// (excluye dwell en estaciones principales).
		/// </summary>
		public static IReadOnlyList<TrackOccupationInterval> GetTrackOccupationsInCanton(
			TimeSpan departure,
			Asimilation asim,
			long pk0,
			long pkf)
		{
			List<TrackOccupationInterval> intervals = new List<TrackOccupationInterval>();
			if (asim is null)
			{
				return intervals;
			}

			long pathMin = Math.Min(asim.Origin.PK, asim.Destination.PK);
			long pathMax = Math.Max(asim.Origin.PK, asim.Destination.PK);
			long c0 = Math.Max(pk0, pathMin);
			long c1 = Math.Min(pkf, pathMax);
			if (c1 <= c0)
			{
				return intervals;
			}

			// Extremos del tramo de vía en orden de marcha.
			long enterPk = asim.Sense == CirculationSense.IncreasingPk ? c0 : c1;
			long exitPk = asim.Sense == CirculationSense.IncreasingPk ? c1 : c0;

			// Puntos de corte: entrada, paradas principales con dwell en el interior, salida.
			List<long> cutPks = new List<long>();
			cutPks.Add(enterPk);

			int si = 0;
			while (si < asim.Stops.Count)
			{
				AsimilationStop stop = asim.Stops[si];
				if (stop.Dwell > TimeSpan.Zero
					&& StationClassification.IsPrincipalStation(stop.Placement.Station)
					&& IsStrictlyBetweenOnPath(stop.PK, enterPk, exitPk, asim.Sense))
				{
					cutPks.Add(stop.PK);
				}

				si++;
			}

			cutPks.Add(exitPk);

			// Segmentos de movimiento entre cortes: sale de Pi (tras dwell) → llega a Pi+1 (antes dwell).
			int i = 0;
			while (i < cutPks.Count - 1)
			{
				long pkA = cutPks[i];
				long pkB = cutPks[i + 1];
				if (pkA == pkB)
				{
					i++;
					continue;
				}

				TimeSpan? relStart = RelativeTrackStartAtPk(asim, pkA);
				TimeSpan? relEnd = RelativeTrackEndAtPk(asim, pkB);
				if (relStart.HasValue && relEnd.HasValue && relEnd.Value > relStart.Value)
				{
					intervals.Add(new TrackOccupationInterval(
						departure + relStart.Value,
						departure + relEnd.Value));
				}

				i++;
			}

			return intervals;
		}

		/// <summary>
		/// Compatibilidad mono-eje: proyecta el eje a <see cref="RouteView.FromAxis"/>.
		/// </summary>
		public static IReadOnlyList<CantonOccupationRect> BuildOccupations(Mesh mesh, Axis axis)
		{
			return BuildOccupations(mesh, RouteView.FromAxis(axis));
		}

		/// <summary>
		/// Una asimilación es visible en la vista si:
		/// - recorre el mismo corredor (firma igual o inversa), o
		/// - comparte tramo físico de eje con la vista (p. ej. trenes T3 en la parte
		///   Palma–Enllaç de la vista T3+T2).
		/// No basta con el id corto de vista.
		/// </summary>
		public static bool IsVisibleOnView(Asimilation asim, RouteView view)
		{
			if (asim is null || view is null)
			{
				return false;
			}

			if (asim.View.IsSameOrReversePath(view))
			{
				return true;
			}

			return view.OverlapsPhysically(asim.View);
		}

		/// <summary>
		/// Detecta intersecciones de ocupaciones incompatibles en <paramref name="view"/>
		/// (misma regla que el planificador: cruce opuesto en doble vía permitido).
		/// </summary>
		public static IReadOnlyList<OccupationConflict> FindHardConflicts(Mesh mesh, RouteView view)
		{
			if (mesh is null)
			{
				throw new ArgumentNullException(nameof(mesh));
			}

			if (view is null)
			{
				throw new ArgumentNullException(nameof(view));
			}

			IReadOnlyList<CantonOccupationRect> occupations = BuildOccupations(mesh, view);
			Dictionary<string, Circulation> byId = new Dictionary<string, Circulation>(StringComparer.Ordinal);
			int ci = 0;
			while (ci < mesh.Circulations.Count)
			{
				Circulation c = mesh.Circulations[ci];
				if (!byId.ContainsKey(c.Id))
				{
					byId[c.Id] = c;
				}

				ci++;
			}

			List<OccupationConflict> conflicts = new List<OccupationConflict>();
			int i = 0;
			while (i < occupations.Count)
			{
				CantonOccupationRect a = occupations[i];
				int j = i + 1;
				while (j < occupations.Count)
				{
					CantonOccupationRect b = occupations[j];
					// Mismo tren: no es conflicto
					if (string.Equals(a.CirculationId, b.CirculationId, StringComparison.Ordinal))
					{
						j++;
						continue;
					}

					CantonOccupationRect? overlap;
					if (!a.TryIntersect(b, out overlap) || overlap is null)
					{
						j++;
						continue;
					}

					Circulation? ca;
					Circulation? cb;
					byId.TryGetValue(a.CirculationId, out ca);
					byId.TryGetValue(b.CirculationId, out cb);

					bool opposite = ca is not null && cb is not null
						&& ArePhysicallyOpposite(ca.Asimilation, cb.Asimilation);
					int tracks = MaxTrackCountInCanton(view, overlap.PkStart, overlap.PkEnd);
					if (opposite && tracks >= 2)
					{
						// Cruce en doble vía: compatible
						j++;
						continue;
					}

					string kind = opposite && tracks < 2 ? "cruce en vía única" : "acantonamiento";
					conflicts.Add(new OccupationConflict(
						a.CirculationId,
						b.CirculationId,
						overlap,
						kind));
					j++;
				}

				i++;
			}

			return conflicts;
		}

		private static void AppendOccupationsForCanton(
			List<CantonOccupationRect> result,
			string circulationId,
			string displayViewId,
			long displayPk0,
			long displayPkf,
			TimeSpan departure,
			Asimilation asim,
			RouteView displayView)
		{
			// Fronteras del diagrama (displayView) → PK de la asimilación (puede ser camino inverso).
			long localPk0;
			long localPkf;
			if (!asim.View.TryMapCantonIntervalFrom(displayView, displayPk0, displayPkf, out localPk0, out localPkf))
			{
				// Sin corredor compartido: no hay ocupación en este cantón.
				return;
			}

			IReadOnlyList<TrackOccupationInterval> intervals =
				GetTrackOccupationsInCanton(departure, asim, localPk0, localPkf);
			int i = 0;
			while (i < intervals.Count)
			{
				TrackOccupationInterval iv = intervals[i];
				if (iv.Exit > iv.Enter)
				{
					// El eje espacial del rectángulo sigue siendo el de la vista de dibujo.
					result.Add(new CantonOccupationRect(
						circulationId,
						displayViewId,
						displayPk0,
						displayPkf,
						iv.Enter,
						iv.Exit));
				}

				i++;
			}
		}

		/// <summary>
		/// Inicio de ocupación de vía al abandonar el PK (tras dwell de estación principal si aplica).
		/// </summary>
		private static TimeSpan? RelativeTrackStartAtPk(Asimilation asim, long pk)
		{
			return asim.TimeDepartByPK(pk);
		}

		/// <summary>
		/// Fin de ocupación de vía al llegar al PK (antes del dwell de estación principal si aplica).
		/// </summary>
		private static TimeSpan? RelativeTrackEndAtPk(Asimilation asim, long pk)
		{
			return asim.TimeArriveByPK(pk);
		}

		private static bool IsStrictlyBetweenOnPath(
			long pk,
			long enterPk,
			long exitPk,
			CirculationSense sense)
		{
			if (sense == CirculationSense.IncreasingPk)
			{
				return pk > enterPk && pk < exitPk;
			}

			return pk < enterPk && pk > exitPk;
		}

		private static bool IntervalOverlapsPath(long pk0, long pkf, Asimilation asim)
		{
			long a0 = Math.Min(asim.Origin.PK, asim.Destination.PK);
			long a1 = Math.Max(asim.Origin.PK, asim.Destination.PK);
			return pk0 < a1 && pkf > a0;
		}

		/// <summary>
		/// Sentidos físicos opuestos en el terreno (no solo CirculationSense local de cada vista).
		/// En caminos multi-eje la vuelta tiene firma inversa y a menudo Sense=Increasing en ambas:
		/// hay que tratarlo como opuestos.
		/// </summary>
		public static bool ArePhysicallyOpposite(Asimilation a, Asimilation b)
		{
			if (a is null || b is null)
			{
				return false;
			}

			if (a.View.IsSamePath(b.View))
			{
				return a.Sense != b.Sense;
			}

			if (a.View.IsReversePath(b.View))
			{
				// Vistas invertidas: Increasing en ambas = sentidos físicos opuestos.
				return a.Sense == b.Sense;
			}

			return InferOppositeOnSharedPhysicalAxis(a, b);
		}

		/// <summary>
		/// Máximo de vías a lo largo del cantón en coords de la vista (incluye el extremo final).
		/// </summary>
		public static int MaxTrackCountInCanton(RouteView view, long pk0, long pkf)
		{
			if (view is null)
			{
				return 1;
			}

			if (pkf < pk0)
			{
				long swap = pk0;
				pk0 = pkf;
				pkf = swap;
			}

			int max = view.GetTrackCountAt(pk0);
			if (pkf > pk0)
			{
				int atEnd = view.GetTrackCountAt(pkf - 1);
				if (atEnd > max)
				{
					max = atEnd;
				}

				// También el PK de estación en el extremo (a menudo frontera de doble vía).
				int atFrontier = view.GetTrackCountAt(pkf);
				if (atFrontier > max)
				{
					max = atFrontier;
				}
			}

			long mid = (pk0 + pkf) / 2;
			int atMid = view.GetTrackCountAt(mid);
			if (atMid > max)
			{
				max = atMid;
			}

			return max;
		}

		private static bool InferOppositeOnSharedPhysicalAxis(Asimilation a, Asimilation b)
		{
			int i = 0;
			while (i < a.View.Legs.Count)
			{
				RouteLeg legA = a.View.Legs[i];
				if (!AsimUsesRouteLeg(a, legA))
				{
					i++;
					continue;
				}

				int j = 0;
				while (j < b.View.Legs.Count)
				{
					RouteLeg legB = b.View.Legs[j];
					if (!string.Equals(legA.Axis.Id, legB.Axis.Id, StringComparison.Ordinal)
						|| !AsimUsesRouteLeg(b, legB))
					{
						j++;
						continue;
					}

					bool aInc = TravelIncreasesAxisPk(a, legA);
					bool bInc = TravelIncreasesAxisPk(b, legB);
					return aInc != bInc;
				}

				i++;
			}

			// Sin eje compartido usable: no afirmar oposición.
			return false;
		}

		private static bool AsimUsesRouteLeg(Asimilation asim, RouteLeg leg)
		{
			long o = Math.Min(asim.Origin.PK, asim.Destination.PK);
			long d = Math.Max(asim.Origin.PK, asim.Destination.PK);
			return o < leg.RoutePkEnd && d > leg.RoutePk0;
		}

		/// <summary>
		/// True si al avanzar el tren por la asimilación el PK de eje de este tramo crece.
		/// </summary>
		private static bool TravelIncreasesAxisPk(Asimilation asim, RouteLeg leg)
		{
			// Sentido Increasing en la vista: se recorre el tramo AxisFrom → AxisTo.
			if (asim.Sense == CirculationSense.IncreasingPk)
			{
				return leg.AxisPkIncreasing;
			}

			return !leg.AxisPkIncreasing;
		}
	}
}
