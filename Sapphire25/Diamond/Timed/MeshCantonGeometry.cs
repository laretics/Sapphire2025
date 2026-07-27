using System;
using System.Collections.Generic;
using Diamond.Motion;
using Diamond.Topo;

namespace Diamond.Timed
{
	/// <summary>
	/// Helper de geometría de acantonamiento sobre una <see cref="Mesh"/>:
	/// cada ocupación de cantón por un tren es un rectángulo tiempo × espacio (PK de ruta).
	/// </summary>
	public static class MeshCantonGeometry
	{
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
				// Sin fronteras: un solo “cantón” por circulación = bounding box del trayecto.
				int ci = 0;
				while (ci < mesh.Circulations.Count)
				{
					Circulation c = mesh.Circulations[ci];
					if (!IsVisibleOnView(c.Asimilation, view))
					{
						ci++;
						continue;
					}

					long pk0 = Math.Min(c.Asimilation.Origin.PK, c.Asimilation.Destination.PK);
					long pk1 = Math.Max(c.Asimilation.Origin.PK, c.Asimilation.Destination.PK);
					result.Add(new CantonOccupationRect(
						c.Id,
						view.Id,
						pk0,
						pk1,
						c.Departure,
						c.Arrival));
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
					if (!IntervalOverlapsPath(pk0, pkf, asim))
					{
						f++;
						continue;
					}

					TimeSpan? enter = AbsoluteEnter(circulation.Departure, asim, pk0, pkf);
					TimeSpan? exit = AbsoluteExit(circulation.Departure, asim, pk0, pkf);
					if (enter.HasValue && exit.HasValue && exit.Value > enter.Value)
					{
						result.Add(new CantonOccupationRect(
							circulation.Id,
							view.Id,
							pk0,
							pkf,
							enter.Value,
							exit.Value));
					}

					f++;
				}

				cIndex++;
			}

			return result;
		}

		/// <summary>
		/// Compatibilidad mono-eje: proyecta el eje a <see cref="RouteView.FromAxis"/>.
		/// </summary>
		public static IReadOnlyList<CantonOccupationRect> BuildOccupations(Mesh mesh, Axis axis)
		{
			return BuildOccupations(mesh, RouteView.FromAxis(axis));
		}

		/// <summary>
		/// Una asimilación es visible en la vista solo si recorre exactamente el mismo camino
		/// (misma firma de tramos/PK). No basta con el id corto de vista.
		/// </summary>
		public static bool IsVisibleOnView(Asimilation asim, RouteView view)
		{
			if (asim is null || view is null)
			{
				return false;
			}

			return asim.View.IsSamePath(view);
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
						&& ca.Asimilation.Sense != cb.Asimilation.Sense;
					int tracks = view.GetTrackCountAt(overlap.PkStart);
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

		private static bool IntervalOverlapsPath(long pk0, long pkf, Asimilation asim)
		{
			long a0 = Math.Min(asim.Origin.PK, asim.Destination.PK);
			long a1 = Math.Max(asim.Origin.PK, asim.Destination.PK);
			return pk0 < a1 && pkf > a0;
		}

		private static TimeSpan? AbsoluteEnter(TimeSpan dep, Asimilation asim, long pk0, long pkf)
		{
			long pathMin = Math.Min(asim.Origin.PK, asim.Destination.PK);
			long pathMax = Math.Max(asim.Origin.PK, asim.Destination.PK);
			long c0 = Math.Max(pk0, pathMin);
			long c1 = Math.Min(pkf, pathMax);
			if (c1 <= c0)
			{
				return null;
			}

			long enterPk = asim.Sense == CirculationSense.IncreasingPk ? c0 : c1;
			TimeSpan? rel = asim.TimeByPK(enterPk);
			if (!rel.HasValue)
			{
				return null;
			}

			return dep + rel.Value;
		}

		private static TimeSpan? AbsoluteExit(TimeSpan dep, Asimilation asim, long pk0, long pkf)
		{
			long pathMin = Math.Min(asim.Origin.PK, asim.Destination.PK);
			long pathMax = Math.Max(asim.Origin.PK, asim.Destination.PK);
			long c0 = Math.Max(pk0, pathMin);
			long c1 = Math.Min(pkf, pathMax);
			if (c1 <= c0)
			{
				return null;
			}

			long exitPk = asim.Sense == CirculationSense.IncreasingPk ? c1 : c0;
			TimeSpan? rel = asim.TimeByPK(exitPk);
			if (!rel.HasValue)
			{
				return null;
			}

			return dep + rel.Value;
		}
	}
}
