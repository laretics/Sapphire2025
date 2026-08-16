using System;
using System.Collections.Generic;
using Diamond.Basis;

namespace Diamond.Topo
{
	/// <summary>
	/// Aplana capas de limitaciones solapadas a tramos disjuntos con la V más restrictiva.
	/// El almacén puede guardar tramos anidados; la representación usa este resultado.
	/// </summary>
	public static class SpeedLimitFlattener
	{
		public static IReadOnlyList<SpeedLimitSpan> Flatten(SpeedLimitMap map)
		{
			if (map is null)
			{
				throw new ArgumentNullException(nameof(map));
			}

			SortedSet<long> cuts = new SortedSet<long>();
			foreach (KeyValuePair<int, AxisVectorFlex> pair in map.BySpeed)
			{
				IReadOnlyList<Lineal<long, LongAxis>> lineals = pair.Value.Lineals;
				int i = 0;
				while (i < lineals.Count)
				{
					cuts.Add(lineals[i].PK);
					cuts.Add(lineals[i].PKEnd);
					i++;
				}
			}

			return WalkCuts(cuts, map);
		}

		/// <summary>
		/// Aplana temporales. <paramref name="track"/> filtra: nulo = todas las vías
		/// (todas las capas, como si se superpusieran); si hay vía, incluye esa y <see cref="TemporaryLimitTrack.Both"/>.
		/// </summary>
		public static IReadOnlyList<SpeedLimitSpan> FlattenTemporary(
			IReadOnlyList<TemporarySpeedLimit> limits,
			TemporaryLimitTrack? track)
		{
			SpeedLimitMap map = new SpeedLimitMap();
			AddTemporary(map, limits, track);
			return Flatten(map);
		}

		/// <summary>
		/// Anida temporales con limitaciones fijas (y opcionalmente el techo del eje).
		/// El resultado puede tener más cortes que las temporales solas.
		/// </summary>
		public static IReadOnlyList<SpeedLimitSpan> FlattenCombined(
			SpeedLimitMap? fixedLimits,
			IReadOnlyList<TemporarySpeedLimit> temporary,
			TemporaryLimitTrack? track)
		{
			SpeedLimitMap map = new SpeedLimitMap();
			if (fixedLimits is not null)
			{
				CopyInto(fixedLimits, map);
			}

			AddTemporary(map, temporary, track);
			return Flatten(map);
		}

		public static void AddTemporary(
			SpeedLimitMap map,
			IReadOnlyList<TemporarySpeedLimit> limits,
			TemporaryLimitTrack? track)
		{
			if (map is null)
			{
				throw new ArgumentNullException(nameof(map));
			}

			if (limits is null)
			{
				return;
			}

			int i = 0;
			while (i < limits.Count)
			{
				TemporarySpeedLimit limit = limits[i];
				if (AppliesToTrack(limit.Track, track))
				{
					map.Add(limit.Speed, limit.PK, limit.PKEnd);
				}

				i++;
			}
		}

		public static bool AppliesToTrack(TemporaryLimitTrack limitTrack, TemporaryLimitTrack? filter)
		{
			if (!filter.HasValue)
			{
				return true;
			}

			if (limitTrack == TemporaryLimitTrack.Both)
			{
				return true;
			}

			return limitTrack == filter.Value;
		}

		private static void CopyInto(SpeedLimitMap source, SpeedLimitMap target)
		{
			foreach (KeyValuePair<int, AxisVectorFlex> pair in source.BySpeed)
			{
				IReadOnlyList<Lineal<long, LongAxis>> lineals = pair.Value.Lineals;
				int i = 0;
				while (i < lineals.Count)
				{
					target.Add(pair.Key, lineals[i].PK, lineals[i].PKEnd);
					i++;
				}
			}
		}

		private static IReadOnlyList<SpeedLimitSpan> WalkCuts(SortedSet<long> cuts, SpeedLimitMap map)
		{
			List<long> points = new List<long>(cuts.Count);
			foreach (long pk in cuts)
			{
				points.Add(pk);
			}

			List<SpeedLimitSpan> salida = new List<SpeedLimitSpan>();
			int i = 0;
			while (i + 1 < points.Count)
			{
				long pk0 = points[i];
				long pkf = points[i + 1];
				if (pkf > pk0)
				{
					int? speed = map.GetMinSpeedAt(pk0);
					if (speed.HasValue)
					{
						if (salida.Count > 0
							&& salida[salida.Count - 1].Speed == speed.Value
							&& salida[salida.Count - 1].PKEnd == pk0)
						{
							salida[salida.Count - 1].PKEnd = pkf;
						}
						else
						{
							salida.Add(new SpeedLimitSpan(pk0, pkf, speed.Value));
						}
					}
				}

				i++;
			}

			return salida;
		}
	}
}
