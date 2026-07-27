using System;
using System.Collections.Generic;
using System.Text;

namespace Diamond.Topo
{
	/// <summary>
	/// Vista lineal de ruta: uno o varios tramos de ejes físicos concatenados en un PK de ruta continuo.
	/// Es el soporte de la malla (eje vertical): asimilaciones, cantones y render se expresan en este PK.
	/// </summary>
	public sealed class RouteView
	{
		private readonly string mvarId;
		private readonly string mvarName;
		private readonly List<RouteLeg> mcolLegs;
		private readonly List<StationOnRoute> mcolStations;
		private readonly List<long> mcolCantonFrontiers;
		private readonly long mvarPk0;
		private readonly long mvarLength;
		private readonly int mvarVmax;

		private RouteView(
			string id,
			string name,
			List<RouteLeg> legs,
			List<StationOnRoute> stations,
			List<long> cantonFrontiers,
			long pk0,
			long length,
			int vmax)
		{
			mvarId = id ?? string.Empty;
			mvarName = name ?? string.Empty;
			mcolLegs = legs;
			mcolStations = stations;
			mcolCantonFrontiers = cantonFrontiers;
			mvarPk0 = pk0;
			mvarLength = length;
			mvarVmax = vmax;
		}

		public string Id
		{
			get { return mvarId; }
		}

		public string Name
		{
			get { return mvarName; }
		}

		public IReadOnlyList<RouteLeg> Legs
		{
			get { return mcolLegs; }
		}

		/// <summary>
		/// Estaciones a lo largo de la ruta (PK de ruta), ordenadas.
		/// </summary>
		public IReadOnlyList<StationOnRoute> Stations
		{
			get { return mcolStations; }
		}

		/// <summary>
		/// Fronteras de cantón proyectadas al PK de ruta (ordenadas, únicas).
		/// </summary>
		public IReadOnlyList<long> CantonFrontiers
		{
			get { return mcolCantonFrontiers; }
		}

		/// <summary>
		/// Origen de la vista en PK de ruta (normalmente 0, o el PK del eje en vistas de un solo tramo completo).
		/// </summary>
		public long PK
		{
			get { return mvarPk0; }
		}

		public long Length
		{
			get { return mvarLength; }
		}

		public long PKEnd
		{
			get { return mvarPk0 + mvarLength; }
		}

		/// <summary>
		/// Vmax representativo (máximo de los ejes de los tramos).
		/// </summary>
		public int Vmax
		{
			get { return mvarVmax; }
		}

		/// <summary>
		/// Vista de un eje completo: el PK de ruta coincide con el PK del eje.
		/// </summary>
		public static RouteView FromAxis(Axis axis)
		{
			if (axis is null)
			{
				throw new ArgumentNullException(nameof(axis));
			}

			long axisFrom = axis.PK;
			long axisTo = axis.PKEnd;
			if (axisTo < axisFrom)
			{
				long swap = axisFrom;
				axisFrom = axisTo;
				axisTo = swap;
			}

			if (axisTo == axisFrom)
			{
				throw new InvalidOperationException(
					"El eje '" + axis.Id + "' no tiene longitud usable para una vista.");
			}

			RouteLeg leg = new RouteLeg(axis, axisFrom, axisTo, routePk0: axisFrom);
			List<RouteLeg> legs = new List<RouteLeg>();
			legs.Add(leg);

			return Build(
				id: axis.Id,
				name: axis.Name,
				legs: legs,
				routePk0: axisFrom,
				preserveAxisStationOrder: true);
		}

		/// <summary>
		/// Concatena tramos (en orden de marcha de la vista). Cada ítem: eje + PK de eje inicio + PK de eje fin.
		/// El PK de ruta empieza en 0 y avanza por longitudes.
		/// </summary>
		public static RouteView Concat(
			string id,
			string name,
			IReadOnlyList<(Axis Axis, long FromPk, long ToPk)> segments)
		{
			if (string.IsNullOrWhiteSpace(id))
			{
				throw new ArgumentException("El id de la vista no puede ser vacío.", nameof(id));
			}

			if (segments is null || segments.Count == 0)
			{
				throw new ArgumentException("La vista necesita al menos un tramo.", nameof(segments));
			}

			List<RouteLeg> legs = new List<RouteLeg>();
			long routeCursor = 0L;
			int index = 0;
			while (index < segments.Count)
			{
				(Axis axis, long fromPk, long toPk) = segments[index];
				if (axis is null)
				{
					throw new ArgumentException("Tramo " + index + ": eje nulo.", nameof(segments));
				}

				if (fromPk == toPk)
				{
					throw new ArgumentException(
						"Tramo " + index + " (" + axis.Id + "): FromPk y ToPk deben diferir.",
						nameof(segments));
				}

				RouteLeg leg = new RouteLeg(axis, fromPk, toPk, routeCursor);
				legs.Add(leg);
				routeCursor = leg.RoutePkEnd;
				index++;
			}

			return Build(id, name ?? id, legs, routePk0: 0L, preserveAxisStationOrder: false);
		}

		/// <summary>
		/// Busca un camino de estaciones sobre uno o varios ejes (BFS por estaciones de enlace).
		/// Primero intenta un único eje; si no, camino multi-eje por estaciones compartidas.
		/// </summary>
		public static bool TryFindPath(
			TopoLayout topo,
			Station from,
			Station to,
			out RouteView? view,
			out StationOnRoute? origin,
			out StationOnRoute? destination)
		{
			view = null;
			origin = null;
			destination = null;

			if (topo is null || from is null || to is null)
			{
				return false;
			}

			// 1) Un solo eje que contenga ambas.
			int axisIndex = 0;
			while (axisIndex < topo.Axes.Count)
			{
				Axis axis = topo.Axes[axisIndex];
				StationOnAxis? o = FindPlacement(axis, from);
				StationOnAxis? d = FindPlacement(axis, to);
				if (o is not null && d is not null && o.PK != d.PK)
				{
					RouteView single = FromAxis(axis);
					StationOnRoute? ro = single.FindStation(from);
					StationOnRoute? rd = single.FindStation(to);
					if (ro is not null && rd is not null)
					{
						view = single;
						origin = ro;
						destination = rd;
						return true;
					}
				}

				axisIndex++;
			}

			// 2) Camino multi-eje: BFS sobre ejes conectados por estaciones de enlace.
			return TryFindMultiAxisPath(topo, from, to, out view, out origin, out destination);
		}

		public StationOnRoute? FindStation(Station station)
		{
			if (station is null)
			{
				return null;
			}

			int index = 0;
			while (index < mcolStations.Count)
			{
				StationOnRoute s = mcolStations[index];
				if (ReferenceEquals(s.Station, station)
					|| string.Equals(s.Station.Id, station.Id, StringComparison.Ordinal)
					|| (s.Station.Avr.Length > 0
						&& string.Equals(s.Station.Avr, station.Avr, StringComparison.OrdinalIgnoreCase)))
				{
					return s;
				}

				index++;
			}

			return null;
		}

		public StationOnRoute? FindStationByRef(string id, string avr, string name)
		{
			int index = 0;
			while (index < mcolStations.Count)
			{
				StationOnRoute s = mcolStations[index];
				Station st = s.Station;
				if (MatchesRef(id, avr, name, st.Id, st.Avr, st.Name))
				{
					return s;
				}

				index++;
			}

			return null;
		}

		public bool TryMapRouteToAxis(long routePk, out Axis? axis, out long axisPk)
		{
			axis = null;
			axisPk = 0L;
			RouteLeg? leg = FindLegForRoutePk(routePk, includeEnd: true);
			if (leg is null)
			{
				return false;
			}

			axis = leg.Axis;
			axisPk = leg.AxisPkFromRoutePk(routePk);
			return true;
		}

		public bool TryMapAxisToRoute(Axis axis, long axisPk, out long routePk)
		{
			routePk = 0L;
			if (axis is null)
			{
				return false;
			}

			int index = 0;
			while (index < mcolLegs.Count)
			{
				RouteLeg leg = mcolLegs[index];
				if (string.Equals(leg.Axis.Id, axis.Id, StringComparison.Ordinal)
					&& leg.ContainsAxisPk(axisPk))
				{
					routePk = leg.RoutePkFromAxisPk(axisPk);
					return true;
				}

				index++;
			}

			return false;
		}

		public int? GetEffectiveSpeedLimit(long routePk)
		{
			Axis? axis;
			long axisPk;
			if (!TryMapRouteToAxis(routePk, out axis, out axisPk) || axis is null)
			{
				return mvarVmax > 0 ? mvarVmax : null;
			}

			return axis.GetEffectiveSpeedLimit(axisPk);
		}

		public int GetTrackCountAt(long routePk)
		{
			Axis? axis;
			long axisPk;
			if (!TryMapRouteToAxis(routePk, out axis, out axisPk) || axis is null)
			{
				return 1;
			}

			return axis.GetTrackCountAt(axisPk);
		}

		public bool AllowsLineCrossingAt(long routePk)
		{
			return GetTrackCountAt(routePk) >= 2;
		}

		/// <summary>
		/// True si la otra vista recorre exactamente los mismos tramos (misma firma de ejes/PK).
		/// No se compara solo el <see cref="Id"/>: varios caminos multi-eje pueden compartir
		/// un id corto (p. ej. "T3+T2") con rangos PK distintos (Palma–SPB vs Inca–SPB).
		/// </summary>
		public bool IsSamePath(RouteView other)
		{
			if (other is null)
			{
				return false;
			}

			return string.Equals(PathSignature(), other.PathSignature(), StringComparison.Ordinal);
		}

		/// <summary>
		/// True si hay solape de ejes físicos (útil para filtrar circulaciones en un render).
		/// </summary>
		public bool SharesPhysicalAxis(RouteView other)
		{
			if (other is null)
			{
				return false;
			}

			int i = 0;
			while (i < mcolLegs.Count)
			{
				int j = 0;
				while (j < other.mcolLegs.Count)
				{
					if (string.Equals(mcolLegs[i].Axis.Id, other.mcolLegs[j].Axis.Id, StringComparison.Ordinal))
					{
						return true;
					}

					j++;
				}

				i++;
			}

			return false;
		}

		/// <summary>
		/// True si esta vista es un solo tramo del eje indicado (id de vista = id de eje).
		/// </summary>
		public bool IsSingleAxis(string axisId)
		{
			return mcolLegs.Count == 1
				&& string.Equals(mcolLegs[0].Axis.Id, axisId, StringComparison.Ordinal);
		}

		public string PathSignature()
		{
			StringBuilder sb = new StringBuilder();
			int index = 0;
			while (index < mcolLegs.Count)
			{
				if (index > 0)
				{
					sb.Append('+');
				}

				RouteLeg leg = mcolLegs[index];
				sb.Append(leg.Axis.Id);
				sb.Append(':');
				sb.Append(leg.AxisFromPk);
				sb.Append('>');
				sb.Append(leg.AxisToPk);
				index++;
			}

			return sb.ToString();
		}

		public override string ToString()
		{
			return mvarId + " · " + mvarName + " [" + PathSignature() + "]";
		}

		private RouteLeg? FindLegForRoutePk(long routePk, bool includeEnd)
		{
			if (mcolLegs.Count == 0)
			{
				return null;
			}

			// Último tramo incluye el extremo final.
			int index = 0;
			while (index < mcolLegs.Count)
			{
				bool last = index == mcolLegs.Count - 1;
				if (mcolLegs[index].ContainsRoutePk(routePk, includeEnd: last || includeEnd))
				{
					// Preferir el tramo que realmente contiene el punto en su semiabierto, salvo el último.
					if (!last && routePk == mcolLegs[index].RoutePkEnd)
					{
						index++;
						continue;
					}

					return mcolLegs[index];
				}

				index++;
			}

			// Clamp a extremos
			if (routePk <= mvarPk0)
			{
				return mcolLegs[0];
			}

			if (routePk >= PKEnd)
			{
				return mcolLegs[mcolLegs.Count - 1];
			}

			return null;
		}

		private static RouteView Build(
			string id,
			string name,
			List<RouteLeg> legs,
			long routePk0,
			bool preserveAxisStationOrder)
		{
			List<StationOnRoute> stations = new List<StationOnRoute>();
			SortedSet<long> frontiers = new SortedSet<long>();
			int vmax = 0;
			long length = 0L;

			int legIndex = 0;
			while (legIndex < legs.Count)
			{
				RouteLeg leg = legs[legIndex];
				length = leg.RoutePkEnd - routePk0;
				if (leg.Axis.Vmax > vmax)
				{
					vmax = leg.Axis.Vmax;
				}

				// Estaciones del tramo (evitar duplicar el nudo de enlace al inicio de tramos siguientes).
				int sIndex = 0;
				while (sIndex < leg.Axis.Stations.Count)
				{
					StationOnAxis placement = leg.Axis.Stations[sIndex];
					if (!leg.ContainsAxisPk(placement.PK))
					{
						sIndex++;
						continue;
					}

					// En tramos siguientes al primero, omitir la estación del extremo inicial
					// (el nudo de enlace ya se añadió como final del tramo anterior).
					if (legIndex > 0 && placement.PK == leg.AxisFromPk)
					{
						sIndex++;
						continue;
					}

					long routePk = leg.RoutePkFromAxisPk(placement.PK);
					stations.Add(new StationOnRoute(placement.Station, routePk, leg, placement.PK));
					sIndex++;
				}

				// Fronteras de cantón del eje físico → PK de ruta.
				int fIndex = 0;
				while (fIndex < leg.Axis.CantonFrontiers.Count)
				{
					long axisPk = leg.Axis.CantonFrontiers[fIndex];
					if (leg.ContainsAxisPk(axisPk))
					{
						frontiers.Add(leg.RoutePkFromAxisPk(axisPk));
					}

					fIndex++;
				}

				// Extremos del tramo siempre acotan.
				frontiers.Add(leg.RoutePk0);
				frontiers.Add(leg.RoutePkEnd);

				legIndex++;
			}

			stations.Sort(static (a, b) => a.PK.CompareTo(b.PK));

			List<long> frontierList = new List<long>();
			foreach (long pk in frontiers)
			{
				frontierList.Add(pk);
			}

			return new RouteView(id, name, legs, stations, frontierList, routePk0, length, vmax);
		}

		private static bool TryFindMultiAxisPath(
			TopoLayout topo,
			Station from,
			Station to,
			out RouteView? view,
			out StationOnRoute? origin,
			out StationOnRoute? destination)
		{
			view = null;
			origin = null;
			destination = null;

			// Grafo: cada "nodo" es una estación; aristas = tramos en un eje entre dos estaciones de ese eje.
			// BFS de caminos cortos (mínimo número de ejes).
			Dictionary<string, Station> stationsByKey = new Dictionary<string, Station>(StringComparer.Ordinal);
			List<(Station A, Station B, Axis Axis, long PkA, long PkB)> edges =
				new List<(Station, Station, Axis, long, long)>();

			int axisIndex = 0;
			while (axisIndex < topo.Axes.Count)
			{
				Axis axis = topo.Axes[axisIndex];
				List<StationOnAxis> placements = new List<StationOnAxis>();
				int p = 0;
				while (p < axis.Stations.Count)
				{
					placements.Add(axis.Stations[p]);
					Station st = axis.Stations[p].Station;
					string key = StationKey(st);
					if (!stationsByKey.ContainsKey(key))
					{
						stationsByKey[key] = st;
					}

					p++;
				}

				int i = 0;
				while (i < placements.Count)
				{
					int j = i + 1;
					while (j < placements.Count)
					{
						edges.Add((
							placements[i].Station,
							placements[j].Station,
							axis,
							placements[i].PK,
							placements[j].PK));
						j++;
					}

					i++;
				}

				axisIndex++;
			}

			string startKey = StationKey(from);
			string goalKey = StationKey(to);
			if (!stationsByKey.ContainsKey(startKey) || !stationsByKey.ContainsKey(goalKey))
			{
				return false;
			}

			// BFS sobre estaciones
			Queue<string> queue = new Queue<string>();
			Dictionary<string, (string PrevStationKey, int EdgeIndex)> cameFrom =
				new Dictionary<string, (string, int)>(StringComparer.Ordinal);

			queue.Enqueue(startKey);
			cameFrom[startKey] = (string.Empty, -1);

			bool found = false;
			while (queue.Count > 0 && !found)
			{
				string current = queue.Dequeue();
				if (string.Equals(current, goalKey, StringComparison.Ordinal))
				{
					found = true;
					break;
				}

				int e = 0;
				while (e < edges.Count)
				{
					(Station a, Station b, Axis axis, long pkA, long pkB) = edges[e];
					string keyA = StationKey(a);
					string keyB = StationKey(b);
					string? next = null;
					if (string.Equals(keyA, current, StringComparison.Ordinal)
						&& !cameFrom.ContainsKey(keyB))
					{
						next = keyB;
					}
					else if (string.Equals(keyB, current, StringComparison.Ordinal)
						&& !cameFrom.ContainsKey(keyA))
					{
						next = keyA;
					}

					if (next is not null)
					{
						cameFrom[next] = (current, e);
						queue.Enqueue(next);
					}

					e++;
				}
			}

			if (!found)
			{
				return false;
			}

			// Reconstruir lista de aristas (estación → estación)
			List<(Station FromSt, Station ToSt, Axis Axis, long FromPk, long ToPk)> pathEdges =
				new List<(Station, Station, Axis, long, long)>();
			string cursor = goalKey;
			while (!string.Equals(cursor, startKey, StringComparison.Ordinal))
			{
				(string prev, int edgeIndex) = cameFrom[cursor];
				if (edgeIndex < 0)
				{
					return false;
				}

				(Station a, Station b, Axis axis, long pkA, long pkB) = edges[edgeIndex];
				string keyA = StationKey(a);
				if (string.Equals(keyA, prev, StringComparison.Ordinal))
				{
					pathEdges.Add((a, b, axis, pkA, pkB));
				}
				else
				{
					pathEdges.Add((b, a, axis, pkB, pkA));
				}

				cursor = prev;
			}

			pathEdges.Reverse();

			// Fusionar aristas consecutivas del mismo eje en un solo tramo continuo.
			List<(Axis Axis, long FromPk, long ToPk)> segments = new List<(Axis, long, long)>();
			int pe = 0;
			while (pe < pathEdges.Count)
			{
				(Station fs, Station ts, Axis axis, long fromPk, long toPk) = pathEdges[pe];
				long segFrom = fromPk;
				long segTo = toPk;
				int q = pe + 1;
				while (q < pathEdges.Count
					&& string.Equals(pathEdges[q].Axis.Id, axis.Id, StringComparison.Ordinal)
					&& pathEdges[q].FromPk == segTo)
				{
					segTo = pathEdges[q].ToPk;
					q++;
				}

				segments.Add((axis, segFrom, segTo));
				pe = q;
			}

			string viewId = BuildPathViewId(segments);
			string viewName = from.Name + " → " + to.Name;
			RouteView built = Concat(viewId, viewName, segments);
			StationOnRoute? ro = built.FindStation(from);
			StationOnRoute? rd = built.FindStation(to);
			if (ro is null || rd is null)
			{
				return false;
			}

			view = built;
			origin = ro;
			destination = rd;
			return true;
		}

		private static string BuildPathViewId(List<(Axis Axis, long FromPk, long ToPk)> segments)
		{
			// Id = firma completa (ejes + PK): evita colisiones T3+T2 Palma–SPB vs Inca–SPB.
			StringBuilder sb = new StringBuilder();
			int index = 0;
			while (index < segments.Count)
			{
				if (index > 0)
				{
					sb.Append('+');
				}

				(Axis axis, long fromPk, long toPk) = segments[index];
				sb.Append(axis.Id);
				sb.Append(':');
				sb.Append(fromPk);
				sb.Append('>');
				sb.Append(toPk);
				index++;
			}

			return sb.ToString();
		}

		private static StationOnAxis? FindPlacement(Axis axis, Station station)
		{
			int index = 0;
			while (index < axis.Stations.Count)
			{
				StationOnAxis placement = axis.Stations[index];
				if (ReferenceEquals(placement.Station, station)
					|| string.Equals(placement.Station.Id, station.Id, StringComparison.Ordinal)
					|| (placement.Station.Avr.Length > 0
						&& string.Equals(placement.Station.Avr, station.Avr, StringComparison.OrdinalIgnoreCase)))
				{
					return placement;
				}

				index++;
			}

			return null;
		}

		/// <summary>
		/// Clave de grafo para path-finding. En samples Onice las estaciones de enlace
		/// (p. ej. Enllaç) tienen ids distintos por eje; priorizamos nombre normalizado
		/// para permitir transferencias multi-eje. Si no hay nombre, caemos a AVR e id.
		/// </summary>
		private static string StationKey(Station station)
		{
			if (!string.IsNullOrWhiteSpace(station.Name))
			{
				return "name:" + station.Name.Trim().ToUpperInvariant();
			}

			if (!string.IsNullOrEmpty(station.Avr))
			{
				return "avr:" + station.Avr.ToUpperInvariant();
			}

			if (!string.IsNullOrEmpty(station.Id))
			{
				return "id:" + station.Id;
			}

			return "anon";
		}

		private static bool MatchesRef(
			string id,
			string avr,
			string name,
			string stationId,
			string stationAvr,
			string stationName)
		{
			if (!string.IsNullOrEmpty(id)
				&& string.Equals(id, stationId, StringComparison.Ordinal))
			{
				return true;
			}

			if (!string.IsNullOrEmpty(avr)
				&& !string.IsNullOrEmpty(stationAvr)
				&& string.Equals(avr, stationAvr, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			if (!string.IsNullOrEmpty(name)
				&& !string.IsNullOrEmpty(stationName)
				&& string.Equals(name, stationName, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			return false;
		}
	}
}
