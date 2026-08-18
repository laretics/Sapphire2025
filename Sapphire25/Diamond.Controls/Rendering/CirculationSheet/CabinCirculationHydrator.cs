using System;
using System.Collections.Generic;
using Diamond.Motion;
using Diamond.Timed;
using Diamond.Topo;
using ProjectCirculation = Diamond.Project.Circulation;
using ProjectModel = Diamond.Project.Project;
using ProjectStationInfo = Diamond.Project.StationInfo;
using ProjectTimedCall = Diamond.Project.TimedCall;
using MotionAsimilation = Diamond.Motion.Asimilation;
using TimedCirculation = Diamond.Timed.Circulation;

namespace Diamond.Controls.Rendering
{
	/// <summary>
	/// Reconstruye una circulación de malla (<see cref="TimedCirculation"/>) a partir
	/// de la circulación de proyecto de cabina y la topología viva.
	/// </summary>
	public static class CabinCirculationHydrator
	{
		/// <summary>Material SFM 8100 (techo 100 km/h) cuando la cabina no aporta catálogo.</summary>
		public static TrainSpecs DefaultCabinSpecs(string? fleetId)
		{
			string id = string.IsNullOrWhiteSpace(fleetId) ? "sfm-8100" : fleetId.Trim();
			string name = string.IsNullOrWhiteSpace(fleetId) ? "CAF 8100" : fleetId.Trim();
			return new TrainSpecs(id, name, 0.8, 0.7, 100.0);
		}

		/// <summary>
		/// Hidrata todas las circulaciones del proyecto del día a una malla Timed
		/// para cruces (Obs.) de la hoja de circulación.
		/// </summary>
		public static Mesh ToMesh(
			ProjectModel project,
			TopoLayout topo,
			TrainSpecs? specs = null)
		{
			if (project is null)
			{
				throw new ArgumentNullException(nameof(project));
			}

			if (topo is null)
			{
				throw new ArgumentNullException(nameof(topo));
			}

			List<TimedCirculation> timed = new List<TimedCirculation>(project.Circulations.Count);
			int i = 0;
			while (i < project.Circulations.Count)
			{
				try
				{
					timed.Add(ToTimed(project.Circulations[i], topo, specs));
				}
				catch
				{
					// Una circulación irresoluble no debe tumbar los cruces del resto.
				}

				i++;
			}

			return Mesh.FromCirculations(timed, project.PlanningDay);
		}

		public static TimedCirculation ToTimed(
			ProjectCirculation circulation,
			TopoLayout topo,
			TrainSpecs? specs = null)
		{
			if (circulation is null)
			{
				throw new ArgumentNullException(nameof(circulation));
			}

			if (topo is null)
			{
				throw new ArgumentNullException(nameof(topo));
			}

			string viewId = circulation.Asimilation.ViewId;
			RouteView? view = RouteViewResolver.TryForCabinCirculation(
				topo,
				viewId,
				circulation.Asimilation.PathSignature,
				circulation.Origin.Id,
				circulation.Destination.Id,
				circulation.Origin.Avr,
				circulation.Destination.Avr);
			if (view is null)
			{
				throw new InvalidOperationException(
					"No se pudo resolver la vista de ruta '"
					+ (string.IsNullOrEmpty(circulation.Asimilation.PathSignature)
						? (viewId ?? string.Empty)
						: circulation.Asimilation.PathSignature)
					+ "' en la topología de cabina.");
			}

			if (circulation.Calls.Count < 2)
			{
				throw new InvalidOperationException(
					"La circulación no tiene origen y destino para generar la hoja.");
			}

			ProjectTimedCall originCall = circulation.Calls[0];
			ProjectTimedCall destCall = circulation.Calls[circulation.Calls.Count - 1];
			int ci = 0;
			while (ci < circulation.Calls.Count)
			{
				ProjectTimedCall call = circulation.Calls[ci];
				if (call.IsOrigin)
				{
					originCall = call;
				}

				if (call.IsDestination)
				{
					destCall = call;
				}

				ci++;
			}

			if (originCall.Pk == destCall.Pk)
			{
				throw new InvalidOperationException(
					"Origen y destino de la circulación tienen el mismo PK.");
			}

			Station originSt = ResolveStation(topo, view, circulation.Origin);
			Station destSt = ResolveStation(topo, view, circulation.Destination);
			long originPk = RoutePkOnView(view, originSt, originCall.Pk);
			long destPk = RoutePkOnView(view, destSt, destCall.Pk);
			if (originPk == destPk)
			{
				throw new InvalidOperationException(
					"Origen y destino de la circulación tienen el mismo PK en la vista resuelta.");
			}

			StationOnAxis origin = new StationOnAxis(originSt, originPk);
			StationOnAxis destination = new StationOnAxis(destSt, destPk);

			List<AsimilationStop> stops = new List<AsimilationStop>();
			int si = 0;
			while (si < circulation.Calls.Count)
			{
				ProjectTimedCall call = circulation.Calls[si];
				si++;
				if (call.IsOrigin || call.IsDestination)
				{
					continue;
				}

				if (call.Dwell <= TimeSpan.Zero)
				{
					continue;
				}

				Station st = ResolveStation(topo, view, call.Station);
				long stopPk = RoutePkOnView(view, st, call.Pk);
				if (stopPk == originPk || stopPk == destPk)
				{
					continue;
				}

				stops.Add(new AsimilationStop(new StationOnAxis(st, stopPk), call.Dwell));
			}

			TrainSpecs material = specs ?? DefaultCabinSpecs(circulation.Asimilation.FleetId);
			MotionAsimilation motion = new MotionAsimilation(view, material, origin, destination, stops);

			string technicalId = circulation.TechnicalId.Length > 0
				? circulation.TechnicalId
				: circulation.Id;
			string? serviceNumber = circulation.HasServiceNumber
				? circulation.ServiceNumber
				: null;
			return new TimedCirculation(
				technicalId,
				circulation.DemandId,
				motion,
				material,
				circulation.Departure,
				circulation.HasColor ? circulation.Color : null,
				serviceNumber);
		}

		private static long RoutePkOnView(RouteView view, Station station, long fallbackPk)
		{
			StationOnRoute? onView = view.FindStation(station);
			if (onView is not null)
			{
				return onView.PK;
			}

			return fallbackPk;
		}

		private static Station ResolveStation(TopoLayout topo, RouteView view, ProjectStationInfo info)
		{
			if (info is null)
			{
				return new Station("?");
			}

			if (!string.IsNullOrEmpty(info.Id))
			{
				Station? byId = topo.FindStationById(info.Id);
				if (byId is not null)
				{
					return byId;
				}
			}

			int i = 0;
			while (i < view.Stations.Count)
			{
				Station st = view.Stations[i].Station;
				if (!string.IsNullOrEmpty(info.Id)
					&& string.Equals(st.Id, info.Id, StringComparison.Ordinal))
				{
					return st;
				}

				if (!string.IsNullOrEmpty(info.Avr)
					&& string.Equals(st.Avr, info.Avr, StringComparison.OrdinalIgnoreCase))
				{
					return st;
				}

				if (!string.IsNullOrEmpty(info.Name)
					&& string.Equals(st.Name, info.Name, StringComparison.OrdinalIgnoreCase))
				{
					return st;
				}

				i++;
			}

			string id = info.Id.Length > 0 ? info.Id : (info.Avr.Length > 0 ? info.Avr : "st");
			Station created = new Station(id);
			created.Name = info.Name;
			created.Avr = info.Avr;
			return created;
		}
	}
}
