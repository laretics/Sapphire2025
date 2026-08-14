using System;
using System.Collections.Generic;
using Diamond.Cabin;

namespace Diamond.Controls.Rendering
{
	/// <summary>
	/// Posición del tren sobre la lista de fronteras de la hoja de circulación.
	/// </summary>
	public readonly struct CabinItineraryCursor
	{
		private CabinItineraryCursor(CabinItineraryCursorKind kind, int frontierIndex, int segmentEndIndex, double progress)
		{
			Kind = kind;
			FrontierIndex = frontierIndex;
			SegmentEndIndex = segmentEndIndex;
			Progress = progress;
		}

		public CabinItineraryCursorKind Kind { get; }

		/// <summary>Índice de estación/dependencia, o inicio del tramo.</summary>
		public int FrontierIndex { get; }

		/// <summary>Fin del tramo (igual a <see cref="FrontierIndex"/> si es punto).</summary>
		public int SegmentEndIndex { get; }

		/// <summary>0…1 a lo largo del tramo actual (0,5 si es un punto).</summary>
		public double Progress { get; }

		public bool IsStation
		{
			get { return Kind == CabinItineraryCursorKind.Station; }
		}

		public bool IsSegment
		{
			get { return Kind == CabinItineraryCursorKind.Segment; }
		}

		public string Key
		{
			get
			{
				if (Kind == CabinItineraryCursorKind.None)
				{
					return "-";
				}

				if (Kind == CabinItineraryCursorKind.Station)
				{
					return "S" + FrontierIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
				}

				return "G" + FrontierIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)
					+ "-" + SegmentEndIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
			}
		}

		public static CabinItineraryCursor None
		{
			get { return new CabinItineraryCursor(CabinItineraryCursorKind.None, -1, -1, 0.0); }
		}

		public static CabinItineraryCursor Resolve(
			IReadOnlyList<CirculationSheetFrontier> frontiers,
			long routePk,
			long stationAreaMeters = CabinItinerary.DefaultStationAreaMeters)
		{
			if (frontiers is null || frontiers.Count == 0)
			{
				return None;
			}

			if (stationAreaMeters < 0)
			{
				stationAreaMeters = 0;
			}

			int nearest = -1;
			long nearestDist = long.MaxValue;
			int i = 0;
			while (i < frontiers.Count)
			{
				long d = Math.Abs(frontiers[i].RoutePk - routePk);
				if (d < nearestDist)
				{
					nearestDist = d;
					nearest = i;
				}

				i++;
			}

			if (nearest >= 0 && nearestDist <= stationAreaMeters)
			{
				return new CabinItineraryCursor(
					CabinItineraryCursorKind.Station,
					nearest,
					nearest,
					0.5);
			}

			int s = 0;
			while (s < frontiers.Count - 1)
			{
				long a = frontiers[s].RoutePk;
				long b = frontiers[s + 1].RoutePk;
				long lo = a < b ? a : b;
				long hi = a > b ? a : b;
				if (routePk >= lo && routePk <= hi)
				{
					double span = hi - lo;
					double u = span < 1.0 ? 0.5 : (routePk - a) / (double)(b - a);
					if (u < 0.0)
					{
						u = 0.0;
					}

					if (u > 1.0)
					{
						u = 1.0;
					}

					return new CabinItineraryCursor(
						CabinItineraryCursorKind.Segment,
						s,
						s + 1,
						u);
				}

				s++;
			}

			// Fuera del recorrido: anclar al extremo más cercano.
			if (nearest < 0)
			{
				return None;
			}

			return new CabinItineraryCursor(
				CabinItineraryCursorKind.Station,
				nearest,
				nearest,
				0.5);
		}
	}

	public enum CabinItineraryCursorKind
	{
		None = 0,
		Station = 1,
		Segment = 2
	}
}
