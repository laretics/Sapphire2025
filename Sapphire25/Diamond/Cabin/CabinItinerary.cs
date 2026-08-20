using System;
using System.Collections.Generic;
using Diamond.Motion;
using ProjectCirculation = Diamond.Project.Circulation;
using ProjectStationInfo = Diamond.Project.StationInfo;
using ProjectTimedCall = Diamond.Project.TimedCall;

namespace Diamond.Cabin
{
	/// <summary>
	/// Cálculo de paradas residuales y estación actual para información al viajero.
	/// </summary>
	public static class CabinItinerary
	{
		/// <summary>
		/// Radio por defecto (metros de PK de ruta) para considerar “en estación”.
		/// </summary>
		public const long DefaultStationAreaMeters = 250;

		/// <summary>
		/// Paradas técnicas en malla (dwell &lt; este umbral) no se anuncian al viajero.
		/// </summary>
		public static readonly TimeSpan MinCommercialDwell = TimeSpan.FromSeconds(10);

		/// <summary>
		/// Estación comercial más cercana al PK actual, no descartada, dentro del radio.
		/// </summary>
		public static ProjectStationInfo? FindCurrentStation(
			ProjectCirculation? circulation,
			long routePk,
			ISet<string>? leftStationIds,
			long stationAreaMeters = DefaultStationAreaMeters)
		{
			if (circulation is null)
			{
				return null;
			}

			ProjectStationInfo? nearest = null;
			long nearestDistance = long.MaxValue;
			int i = 0;
			while (i < circulation.Calls.Count)
			{
				ProjectTimedCall call = circulation.Calls[i];
				if (leftStationIds is not null
					&& leftStationIds.Contains(call.Station.Id))
				{
					i++;
					continue;
				}

				if (!IsCommercial(call))
				{
					i++;
					continue;
				}

				long distance = Math.Abs(call.Pk - routePk);
				if (distance > stationAreaMeters)
				{
					i++;
					continue;
				}

				if (distance < nearestDistance)
				{
					nearestDistance = distance;
					nearest = call.Station;
				}

				i++;
			}

			return nearest;
		}

		/// <summary>
		/// Paradas comerciales aún por hacer desde el PK actual (orden de marcha).
		/// </summary>
		public static IReadOnlyList<ProjectTimedCall> RemainingCommercialCalls(
			ProjectCirculation circulation,
			long routePk,
			bool includeCurrentStation = false)
		{
			if (circulation is null)
			{
				throw new ArgumentNullException(nameof(circulation));
			}

			bool increasing = circulation.Asimilation.Sense == CirculationSense.IncreasingPk;
			List<ProjectTimedCall> result = new List<ProjectTimedCall>();
			int i = 0;
			while (i < circulation.Calls.Count)
			{
				ProjectTimedCall call = circulation.Calls[i];
				if (!IsCommercial(call))
				{
					i++;
					continue;
				}

				bool ahead;
				if (increasing)
				{
					ahead = includeCurrentStation
						? call.Pk >= routePk
						: call.Pk > routePk;
				}
				else
				{
					ahead = includeCurrentStation
						? call.Pk <= routePk
						: call.Pk < routePk;
				}

				if (ahead)
				{
					result.Add(call);
				}

				i++;
			}

			return result;
		}

		/// <summary>
		/// Hora de llegada programada + retraso, por id de estación.
		/// </summary>
		public static Dictionary<string, TimeSpan> ScheduledArrivalsWithDelay(
			ProjectCirculation circulation,
			TimeSpan delay)
		{
			if (circulation is null)
			{
				throw new ArgumentNullException(nameof(circulation));
			}

			Dictionary<string, TimeSpan> map =
				new Dictionary<string, TimeSpan>(StringComparer.Ordinal);
			int i = 0;
			while (i < circulation.Calls.Count)
			{
				ProjectTimedCall call = circulation.Calls[i];
				if (!IsCommercial(call))
				{
					i++;
					continue;
				}

				if (call.Station.Id.Length > 0)
				{
					TimeSpan when = call.Arrival + delay;
					if (when < TimeSpan.Zero)
					{
						when = TimeSpan.Zero;
					}

					map[call.Station.Id] = when;
				}

				i++;
			}

			return map;
		}

		/// <summary>PK de ruta de la estación de origen de la circulación.</summary>
		public static long? OriginRoutePk(ProjectCirculation? circulation)
		{
			if (circulation is null || circulation.Calls.Count == 0)
			{
				return null;
			}

			int i = 0;
			while (i < circulation.Calls.Count)
			{
				if (circulation.Calls[i].IsOrigin)
				{
					return circulation.Calls[i].Pk;
				}

				i++;
			}

			return circulation.Calls[0].Pk;
		}

		/// <summary>PK de ruta de la estación de destino de la circulación.</summary>
		public static long? DestinationRoutePk(ProjectCirculation? circulation)
		{
			if (circulation is null || circulation.Calls.Count == 0)
			{
				return null;
			}

			int i = circulation.Calls.Count - 1;
			while (i >= 0)
			{
				if (circulation.Calls[i].IsDestination)
				{
					return circulation.Calls[i].Pk;
				}

				i--;
			}

			return circulation.Calls[circulation.Calls.Count - 1].Pk;
		}

		public static bool IsCommercial(ProjectTimedCall call)
		{
			if (call is null)
			{
				return false;
			}

			if (call.IsOrigin || call.IsDestination)
			{
				return true;
			}

			if (call.CommercialStop)
			{
				return true;
			}

			return call.Dwell >= MinCommercialDwell;
		}

		/// <summary>
		/// Demora en el PK actual: hora de ahora menos la hora teórica en ese punto.
		/// Positivo = retraso; negativo = adelanto. Durante la parada programada, 0.
		/// </summary>
		public static TimeSpan DelayAtRoutePk(
			ProjectCirculation? circulation,
			long routePk,
			DateTime now)
		{
			if (circulation is null || circulation.Calls.Count == 0)
			{
				return TimeSpan.Zero;
			}

			TimeSpan tod = now.TimeOfDay;
			int i = 0;
			while (i < circulation.Calls.Count)
			{
				ProjectTimedCall call = circulation.Calls[i];
				if (Math.Abs(call.Pk - routePk) <= DefaultStationAreaMeters)
				{
					if (tod >= call.Arrival && tod <= call.Departure)
						return TimeSpan.Zero;
					if (tod < call.Arrival)
						return WrapHalfDay(tod - call.Arrival);
					return WrapHalfDay(tod - call.Departure);
				}

				i++;
			}

			TimeSpan? theoretical = TheoreticalTimeAtPk(circulation, routePk);
			if (!theoretical.HasValue)
				return TimeSpan.Zero;
			return WrapHalfDay(tod - theoretical.Value);
		}

		/// <summary>Hora teórica (reloj civil del día) en un PK de ruta, interpolada entre llamadas.</summary>
		public static TimeSpan? TheoreticalTimeAtPk(ProjectCirculation circulation, long routePk)
		{
			if (circulation is null || circulation.Calls.Count == 0)
				return null;

			IReadOnlyList<ProjectTimedCall> calls = circulation.Calls;
			bool increasing = circulation.Asimilation.Sense == CirculationSense.IncreasingPk;
			ProjectTimedCall first = calls[0];
			ProjectTimedCall last = calls[calls.Count - 1];

			if (increasing)
			{
				if (routePk <= first.Pk)
					return first.Arrival;
				if (routePk >= last.Pk)
					return last.Arrival;
			}
			else
			{
				if (routePk >= first.Pk)
					return first.Arrival;
				if (routePk <= last.Pk)
					return last.Arrival;
			}

			int i = 0;
			while (i < calls.Count - 1)
			{
				ProjectTimedCall a = calls[i];
				ProjectTimedCall b = calls[i + 1];
				bool inSegment = increasing
					? routePk >= a.Pk && routePk <= b.Pk
					: routePk <= a.Pk && routePk >= b.Pk;
				if (!inSegment)
				{
					i++;
					continue;
				}

				long span = b.Pk - a.Pk;
				if (span == 0)
					return a.Departure;

				double t = (double)(routePk - a.Pk) / span;
				if (t < 0)
					t = 0;
				if (t > 1)
					t = 1;
				long ticks = a.Departure.Ticks + (long)((b.Arrival.Ticks - a.Departure.Ticks) * t);
				return TimeSpan.FromTicks(ticks);
			}

			return last.Arrival;
		}

		/// <summary>PK en el que el tren debería estar a la hora dada según malla.</summary>
		public static long? ScheduledPkAtTime(ProjectCirculation? circulation, TimeSpan now)
		{
			if (circulation is null || circulation.Calls.Count == 0)
				return null;

			IReadOnlyList<ProjectTimedCall> calls = circulation.Calls;
			if (now <= calls[0].Departure)
				return calls[0].Pk;
			if (now >= calls[calls.Count - 1].Arrival)
				return calls[calls.Count - 1].Pk;

			int i = 0;
			while (i < calls.Count - 1)
			{
				ProjectTimedCall a = calls[i];
				ProjectTimedCall b = calls[i + 1];
				if (now <= a.Departure)
					return a.Pk;

				if (now <= b.Arrival)
				{
					double spanSec = (b.Arrival - a.Departure).TotalSeconds;
					if (spanSec <= 0)
						return a.Pk;
					double t = (now - a.Departure).TotalSeconds / spanSec;
					if (t < 0)
						t = 0;
					if (t > 1)
						t = 1;
					return a.Pk + (long)((b.Pk - a.Pk) * t);
				}

				i++;
			}

			return calls[calls.Count - 1].Pk;
		}

		/// <summary>Progreso espacial 0..1 entre origen y destino de ruta.</summary>
		public static double SpatialProgress(ProjectCirculation? circulation, long routePk)
		{
			if (circulation is null)
				return 0;
			long? origin = OriginRoutePk(circulation);
			long? dest = DestinationRoutePk(circulation);
			if (!origin.HasValue || !dest.HasValue)
				return 0;
			long span = dest.Value - origin.Value;
			if (span == 0)
				return routePk == dest.Value ? 1 : 0;
			double t = (double)(routePk - origin.Value) / span;
			if (t < 0)
				return 0;
			if (t > 1)
				return 1;
			return t;
		}

		private static TimeSpan WrapHalfDay(TimeSpan delta)
		{
			if (delta > TimeSpan.FromHours(12))
				return delta - TimeSpan.FromDays(1);
			if (delta < TimeSpan.FromHours(-12))
				return delta + TimeSpan.FromDays(1);
			return delta;
		}

		/// <summary>
		/// Ejes de la topología implicados por el ViewId de la asimilación (p. ej. "T3" o "T3+T2").
		/// </summary>
		public static IReadOnlyList<AxisRef> ResolveMissionAxisIds(string? viewId)
		{
			if (string.IsNullOrWhiteSpace(viewId))
			{
				return Array.Empty<AxisRef>();
			}

			string[] parts = viewId.Split(
				new[] { '+', '|', ',', ';' },
				StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			List<AxisRef> list = new List<AxisRef>(parts.Length);
			int i = 0;
			while (i < parts.Length)
			{
				if (parts[i].Length > 0)
				{
					list.Add(new AxisRef(parts[i]));
				}

				i++;
			}

			return list;
		}

		public readonly struct AxisRef
		{
			public AxisRef(string id)
			{
				Id = id ?? string.Empty;
			}

			public string Id { get; }
		}
	}
}
