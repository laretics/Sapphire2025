using System;
using System.Collections.Generic;
using Diamond.Motion;
using Diamond.Topo;

namespace Diamond.Timed
{
	/// <summary>
	/// Helper de geometría de acantonamiento sobre una <see cref="Mesh"/>:
	/// cada ocupación de cantón por un tren es un rectángulo tiempo × espacio.
	/// </summary>
	public static class MeshCantonGeometry
	{
		/// <summary>
		/// Construye todos los rectángulos de ocupación de cantón de las circulaciones
		/// que circulan por <paramref name="axis"/>.
		/// </summary>
		public static IReadOnlyList<CantonOccupationRect> BuildOccupations(Mesh mesh, Axis axis)
		{
			if (mesh is null)
			{
				throw new ArgumentNullException(nameof(mesh));
			}

			if (axis is null)
			{
				throw new ArgumentNullException(nameof(axis));
			}

			List<CantonOccupationRect> result = new List<CantonOccupationRect>();
			IReadOnlyList<long> frontiers = axis.CantonFrontiers;
			if (frontiers.Count < 2)
			{
				// Sin fronteras: un solo “cantón” por circulación = bounding box del trayecto.
				int ci = 0;
				while (ci < mesh.Circulations.Count)
				{
					Circulation c = mesh.Circulations[ci];
					if (!string.Equals(c.Asimilation.Axis.Id, axis.Id, StringComparison.Ordinal))
					{
						ci++;
						continue;
					}

					long pk0 = Math.Min(c.Asimilation.Origin.PK, c.Asimilation.Destination.PK);
					long pk1 = Math.Max(c.Asimilation.Origin.PK, c.Asimilation.Destination.PK);
					result.Add(new CantonOccupationRect(
						c.Id,
						axis.Id,
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
				if (!string.Equals(circulation.Asimilation.Axis.Id, axis.Id, StringComparison.Ordinal))
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
							axis.Id,
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
		/// True si no hay ningún par de rectángulos de circulaciones distintas que se superpongan.
		/// </summary>
		public static bool AllCompatible(IReadOnlyList<CantonOccupationRect> occupations)
		{
			if (occupations is null)
			{
				return true;
			}

			int i = 0;
			while (i < occupations.Count)
			{
				int j = i + 1;
				while (j < occupations.Count)
				{
					if (!string.Equals(occupations[i].CirculationId, occupations[j].CirculationId, StringComparison.Ordinal)
						&& occupations[i].Overlaps(occupations[j]))
					{
						return false;
					}

					j++;
				}

				i++;
			}

			return true;
		}

		/// <summary>
		/// Pares de circulaciones distintas cuyos rectángulos se cruzan (conflictos de modelo rectangular).
		/// </summary>
		public static IReadOnlyList<(CantonOccupationRect A, CantonOccupationRect B)> FindOverlaps(
			IReadOnlyList<CantonOccupationRect> occupations)
		{
			List<(CantonOccupationRect, CantonOccupationRect)> overlaps = new List<(CantonOccupationRect, CantonOccupationRect)>();
			if (occupations is null)
			{
				return overlaps;
			}

			int i = 0;
			while (i < occupations.Count)
			{
				int j = i + 1;
				while (j < occupations.Count)
				{
					if (!string.Equals(occupations[i].CirculationId, occupations[j].CirculationId, StringComparison.Ordinal)
						&& occupations[i].Overlaps(occupations[j]))
					{
						overlaps.Add((occupations[i], occupations[j]));
					}

					j++;
				}

				i++;
			}

			return overlaps;
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
