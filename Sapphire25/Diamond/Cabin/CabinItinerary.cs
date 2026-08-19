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
