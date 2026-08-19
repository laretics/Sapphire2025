using Diamond.Cabin;
using Diamond.Project;
using Diamond.Topo;
using Tourmaline26.Logic;

namespace Tourmaline26.Services
{
	/// <summary>
	/// Recorre la circulación cargada con los tiempos de la malla (dwell + marcha).
	/// No usa Tourmaline Experience: escribe PK, GPS y velocidad de sesión.
	/// </summary>
	public sealed class RouteSimulationService
	{
		public enum SimState
		{
			Stopped = 0,
			Running = 1,
			Paused = 2
		}

		private readonly TourmalineService mvarTourmaline;
		private readonly ILogger<RouteSimulationService> mvarLogger;
		private readonly object mvarLock = new();

		private SimState mvarState = SimState.Stopped;
		private string mvarStatus = "Parado";
		private string? mvarCirculationId;
		private TimeSpan mvarElapsed;
		private DateTime mvarLastTickUtc = DateTime.MinValue;
		private RouteView? mvarView;
		private bool mvarDwelling;

		/// <summary>
		/// En marchas de más de 1 km, si la media de la malla no llega a 70 km/h
		/// (tiempos holgados, arranque/frenada), se muestra 70 para crucero PIS.
		/// </summary>
		private const double LongHopMeters = 1000.0;
		private const double LongHopCruiseKmh = 70.0;

		public RouteSimulationService(
			TourmalineService tourmaline,
			ILogger<RouteSimulationService> logger)
		{
			mvarTourmaline = tourmaline;
			mvarLogger = logger;
		}

		public SimState State
		{
			get { lock (mvarLock) return mvarState; }
		}

		public string Status
		{
			get { lock (mvarLock) return mvarStatus; }
		}

		public TimeSpan Elapsed
		{
			get { lock (mvarLock) return mvarElapsed; }
		}

		public bool IsActive
		{
			get
			{
				SimState state = State;
				return state == SimState.Running || state == SimState.Paused;
			}
		}

		public string StartOrResume()
		{
			lock (mvarLock)
			{
				SessionConfiguration session = mvarTourmaline.SessionConfig;
				if (!session.ServiceMode.Main)
					return SetStatus("Solo en modo servicio.");

				CabinEnvironment? cabin = session.Cabin;
				Circulation? circulation = cabin?.Circulation;
				if (cabin is null || circulation is null || circulation.Calls.Count < 2)
					return SetStatus("Carga una circulación (misión Diamond).");

				if (cabin.Topo is null)
					return SetStatus("No hay topología cargada.");

				if (mvarState == SimState.Paused
					&& string.Equals(mvarCirculationId, circulation.Id, StringComparison.Ordinal))
				{
					mvarState = SimState.Running;
					mvarLastTickUtc = DateTime.UtcNow;
					session.ServiceMode.RouteSimulation = true;
					return SetStatus("Reanudado.");
				}

				Asimilation asim = circulation.Asimilation;
				mvarView = RouteViewResolver.TryForCabinCirculation(
					cabin.Topo,
					asim.ViewId,
					asim.PathSignature,
					asim.Origin.Id,
					asim.Destination.Id,
					asim.Origin.Avr,
					asim.Destination.Avr);

				mvarCirculationId = circulation.Id;
				mvarElapsed = TimeSpan.Zero;
				mvarLastTickUtc = DateTime.UtcNow;
				mvarDwelling = true;
				mvarState = SimState.Running;
				session.ServiceMode.RouteSimulation = true;
				cabin.ResetStationProgress();

				long originPk = CabinItinerary.OriginRoutePk(circulation) ?? circulation.Calls[0].Pk;
				cabin.PK = originPk;
				PlaceAtRoutePk(session, cabin, originPk);
				session.SimulatedSpeed = 0;
				session.InformationMode = Enums.PassengerInformationMode.BeginOfTrip;

				ApplyPoseUnlocked(session, cabin, circulation, mvarElapsed);
				mvarLogger.LogInformation(
					"Simulador de ruta: inicio {Id} en PK origen {Pk}",
					circulation.Id,
					originPk);
				mvarTourmaline.RaisePassengerUpdate();
				mvarTourmaline.RaiseHMIUpdate();
				return SetStatus("En origen (PK " + originPk + ").");
			}
		}

		public string Pause()
		{
			lock (mvarLock)
			{
				if (mvarState != SimState.Running)
					return mvarStatus;

				mvarState = SimState.Paused;
				mvarLastTickUtc = DateTime.MinValue;
				return SetStatus("Pausa.");
			}
		}

		public void Stop()
		{
			lock (mvarLock)
			{
				StopUnlocked("Parado.");
			}
		}

		public void Tick()
		{
			lock (mvarLock)
			{
				SessionConfiguration session = mvarTourmaline.SessionConfig;
				if (!session.ServiceMode.Main)
				{
					if (mvarState != SimState.Stopped)
						StopUnlocked("Modo servicio desactivado.");
					return;
				}

				if (mvarState != SimState.Running)
					return;

				CabinEnvironment? cabin = session.Cabin;
				Circulation? circulation = cabin?.Circulation;
				if (cabin is null || circulation is null
					|| !string.Equals(mvarCirculationId, circulation.Id, StringComparison.Ordinal))
				{
					StopUnlocked("La circulación ya no está cargada.");
					return;
				}

				DateTime now = DateTime.UtcNow;
				if (mvarLastTickUtc == DateTime.MinValue)
					mvarLastTickUtc = now;

				double dt = (now - mvarLastTickUtc).TotalSeconds;
				mvarLastTickUtc = now;
				if (dt <= 0 || dt > 2.0)
					dt = 0.5;

				mvarElapsed += TimeSpan.FromSeconds(dt);
				TimeSpan tripEnd = circulation.Arrival - circulation.Departure;
				if (tripEnd < TimeSpan.Zero)
					tripEnd = TimeSpan.Zero;

				bool finished = false;
				if (mvarElapsed > tripEnd)
				{
					mvarElapsed = tripEnd;
					finished = true;
				}

				ApplyPoseUnlocked(session, cabin, circulation, mvarElapsed);

				if (finished)
				{
					mvarState = SimState.Paused;
					mvarLastTickUtc = DateTime.MinValue;
					SetStatus("Fin de trayecto (F7 para repetir).");
				}
			}
		}

		private void ApplyPoseUnlocked(
			SessionConfiguration session,
			CabinEnvironment cabin,
			Circulation circulation,
			TimeSpan elapsed)
		{
			ResolvePose(circulation, elapsed, out long routePk, out double speedKmh, out string place);
			bool dwelling = speedKmh < 0.5;
			if (mvarDwelling && !dwelling)
				cabin.LeaveCurrentStation();
			mvarDwelling = dwelling;

			cabin.PK = routePk;
			session.SimulatedSpeed = (int)Math.Round(Math.Clamp(speedKmh, 0, 140));

			if (session.CurrentMVBData is not null && session.ServiceMode.MVBDummy)
				session.CurrentMVBData.Speed = session.SimulatedSpeed;

			PlaceAtRoutePk(session, cabin, routePk, speedKmh);

			if (mvarState == SimState.Running)
				SetStatus($"{place} · {speedKmh:0} km/h · {FormatElapsed(elapsed)}");
		}

		private void PlaceAtRoutePk(
			SessionConfiguration session,
			CabinEnvironment cabin,
			long routePk,
			double speedKmh = 0)
		{
			if (mvarView is null
				|| !mvarView.TryMapRouteToAxis(routePk, out Axis? axis, out long axisPk)
				|| axis is null)
			{
				return;
			}

			cabin.LinearLocation.SetManual(axis, axisPk);
			GeoPoint geo = axis.LocationFromPK(axisPk);
			session.CurrentGPSData = new GPSData
			{
				Latitude = geo.Latitude,
				Longitude = geo.Longitude,
				Time = DateTime.UtcNow,
				SpeedKmh = speedKmh,
				SpeedMs = speedKmh / 3.6,
				FixQuality = 1,
				SatellitesUsed = 8,
				HDOP = 1.0
			};
			session.GPSLastUpdate = DateTime.Now;
			session.GPSOK = true;
		}

		private static void ResolvePose(
			Circulation circulation,
			TimeSpan elapsed,
			out long routePk,
			out double speedKmh,
			out string place)
		{
			IReadOnlyList<TimedCall> calls = circulation.Calls;
			TimeSpan origin = circulation.Departure;
			int last = calls.Count - 1;

			if (elapsed <= TimeSpan.Zero)
			{
				routePk = calls[0].Pk;
				speedKmh = 0;
				place = calls[0].Station.DisplayCode;
				return;
			}

			int i = 0;
			while (i < last)
			{
				TimedCall here = calls[i];
				TimedCall next = calls[i + 1];
				TimeSpan arriveHere = here.Arrival - origin;
				TimeSpan departHere = here.Departure - origin;
				TimeSpan arriveNext = next.Arrival - origin;

				if (elapsed < arriveHere)
				{
					routePk = here.Pk;
					speedKmh = 0;
					place = here.Station.DisplayCode;
					return;
				}

				if (elapsed < departHere)
				{
					routePk = here.Pk;
					speedKmh = 0;
					place = here.Station.DisplayCode;
					return;
				}

				if (elapsed < arriveNext)
				{
					double run = (arriveNext - departHere).TotalSeconds;
					double u = run <= 0.05 ? 1.0 : (elapsed - departHere).TotalSeconds / run;
					if (u < 0) u = 0;
					if (u > 1) u = 1;
					routePk = here.Pk + (long)Math.Round(u * (next.Pk - here.Pk));
					double meters = Math.Abs(next.Pk - here.Pk);
					speedKmh = run <= 0.05 ? 0 : (meters / run) * 3.6;
					if (meters > LongHopMeters && speedKmh < LongHopCruiseKmh)
						speedKmh = LongHopCruiseKmh;
					place = here.Station.DisplayCode + " → " + next.Station.DisplayCode;
					return;
				}

				i++;
			}

			routePk = calls[last].Pk;
			speedKmh = 0;
			place = calls[last].Station.DisplayCode;
		}

		private void StopUnlocked(string status)
		{
			mvarState = SimState.Stopped;
			mvarCirculationId = null;
			mvarView = null;
			mvarElapsed = TimeSpan.Zero;
			mvarLastTickUtc = DateTime.MinValue;
			mvarDwelling = false;
			mvarTourmaline.SessionConfig.ServiceMode.RouteSimulation = false;
			SetStatus(status);
		}

		private string SetStatus(string status)
		{
			mvarStatus = status;
			return status;
		}

		private static string FormatElapsed(TimeSpan elapsed)
		{
			int total = Math.Max(0, (int)elapsed.TotalSeconds);
			return string.Format("{0}:{1:00}", total / 60, total % 60);
		}
	}
}
