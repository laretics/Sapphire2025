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
		/// <summary>En simulación todas las paradas duran esto, da igual el dwell de malla.</summary>
		private const double SimDwellSeconds = 15.0;
		/// <summary>Salto F5: metros finales del trayecto.</summary>
		public const long JumpTailMeters = 2000;

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
				if (!session.ServiceMode.RouteSimKeysEnabled)
					return SetStatus("Activa Demo en modo servicio.");

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

		/// <summary>
		/// Coloca el tren a <paramref name="remainingMeters"/> del destino
		/// (por defecto 2000 m) y deja la simulación en marcha.
		/// </summary>
		public string JumpToLastMeters(long remainingMeters = JumpTailMeters)
		{
			lock (mvarLock)
			{
				string? ready = EnsureCirculationUnlocked();
				if (ready is not null)
					return ready;

				SessionConfiguration session = mvarTourmaline.SessionConfig;
				CabinEnvironment cabin = session.Cabin!;
				Circulation circulation = cabin.Circulation!;

				if (!TryElapsedForRemainingMeters(circulation, remainingMeters, out TimeSpan elapsed, out long routePk))
					return SetStatus("No se pudo calcular el tramo final.");

				if (!EnsureViewUnlocked(cabin, circulation))
					return SetStatus("No hay topología o vista de ruta.");

				mvarCirculationId = circulation.Id;
				mvarElapsed = elapsed;
				mvarLastTickUtc = DateTime.UtcNow;
				mvarDwelling = false;
				mvarState = SimState.Running;
				session.ServiceMode.RouteSimulation = true;
				cabin.ResetStationProgress();
				session.InformationMode = Enums.PassengerInformationMode.NextStopInfo;

				ApplyPoseUnlocked(session, cabin, circulation, mvarElapsed);
				mvarLogger.LogInformation(
					"Simulador de ruta: salto a {Meters} m del destino (PK {Pk}, t={Elapsed})",
					remainingMeters,
					routePk,
					FormatElapsed(elapsed));
				mvarTourmaline.RaisePassengerUpdate();
				mvarTourmaline.RaiseHMIUpdate();
				return SetStatus($"Salto a {remainingMeters} m del destino (PK {routePk}).");
			}
		}

		public void Stop()
		{
			lock (mvarLock)
			{
				StopUnlocked("Parado.");
			}
		}

		/// <summary>
		/// Sale de cualquier simulación (ruta F3–F5 y Demo 3/4) y deja MVB/GPS reales.
		/// </summary>
		public string Abandon()
		{
			lock (mvarLock)
			{
				SessionConfiguration session = mvarTourmaline.SessionConfig;
				bool any = mvarState != SimState.Stopped
					|| session.ServiceMode.AnySimulation
					|| session.SimulatedSpeed != 0
					|| session.CurrentNeutralSpeed != 0;

				StopUnlocked("Modo real.");
				session.ServiceMode.DemoMode = false;
				RestoreLiveTelemetry(session);

				if (!any)
					return SetStatus("Ya en modo real.");

				mvarLogger.LogInformation("Simulación abandonada; vuelta a MVB/GPS reales.");
				mvarTourmaline.RaisePassengerUpdate();
				mvarTourmaline.RaiseHMIUpdate();
				return SetStatus("Modo real (MVB/GPS).");
			}
		}

		public void Tick()
		{
			lock (mvarLock)
			{
				SessionConfiguration session = mvarTourmaline.SessionConfig;
				if (!session.ServiceMode.RouteSimKeysEnabled)
				{
					if (mvarState != SimState.Stopped)
						StopUnlocked(session.ServiceMode.Main
							? "Demo desactivado."
							: "Modo servicio desactivado.");
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
				TimeSpan tripEnd = SimTripDuration(circulation);

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
					SetStatus("Fin de trayecto (F3 para repetir).");
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

		private static TimeSpan ScheduledRun(TimedCall here, TimedCall next)
		{
			TimeSpan run = next.Arrival - here.Departure;
			return run < TimeSpan.Zero ? TimeSpan.Zero : run;
		}

		private static TimeSpan SimTripDuration(Circulation circulation)
		{
			IReadOnlyList<TimedCall> calls = circulation.Calls;
			if (calls.Count == 0)
				return TimeSpan.Zero;

			double seconds = SimDwellSeconds * calls.Count;
			int i = 0;
			while (i < calls.Count - 1)
			{
				seconds += ScheduledRun(calls[i], calls[i + 1]).TotalSeconds;
				i++;
			}

			return TimeSpan.FromSeconds(seconds);
		}

		private static void ResolvePose(
			Circulation circulation,
			TimeSpan elapsed,
			out long routePk,
			out double speedKmh,
			out string place)
		{
			IReadOnlyList<TimedCall> calls = circulation.Calls;
			int last = calls.Count - 1;
			double t = elapsed.TotalSeconds;

			if (t <= 0 || last < 1)
			{
				routePk = calls[0].Pk;
				speedKmh = 0;
				place = calls[0].Station.DisplayCode;
				return;
			}

			double clock = 0;
			int i = 0;
			while (i < last)
			{
				TimedCall here = calls[i];
				TimedCall next = calls[i + 1];
				double dwellEnd = clock + SimDwellSeconds;
				if (t < dwellEnd)
				{
					routePk = here.Pk;
					speedKmh = 0;
					place = here.Station.DisplayCode;
					return;
				}

				double run = ScheduledRun(here, next).TotalSeconds;
				double runEnd = dwellEnd + run;
				if (t < runEnd)
				{
					double u = run <= 0.05 ? 1.0 : (t - dwellEnd) / run;
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

				clock = runEnd;
				i++;
			}

			routePk = calls[last].Pk;
			speedKmh = 0;
			place = calls[last].Station.DisplayCode;
		}

		private string? EnsureCirculationUnlocked()
		{
			SessionConfiguration session = mvarTourmaline.SessionConfig;
			if (!session.ServiceMode.RouteSimKeysEnabled)
				return SetStatus("Activa Demo en modo servicio.");

			CabinEnvironment? cabin = session.Cabin;
			Circulation? circulation = cabin?.Circulation;
			if (cabin is null || circulation is null || circulation.Calls.Count < 2)
				return SetStatus("Carga una circulación (misión Diamond).");
			if (cabin.Topo is null)
				return SetStatus("No hay topología cargada.");
			return null;
		}

		private bool EnsureViewUnlocked(CabinEnvironment cabin, Circulation circulation)
		{
			if (mvarView is not null
				&& string.Equals(mvarCirculationId, circulation.Id, StringComparison.Ordinal))
			{
				return true;
			}

			if (cabin.Topo is null)
				return false;

			Asimilation asim = circulation.Asimilation;
			mvarView = RouteViewResolver.TryForCabinCirculation(
				cabin.Topo,
				asim.ViewId,
				asim.PathSignature,
				asim.Origin.Id,
				asim.Destination.Id,
				asim.Origin.Avr,
				asim.Destination.Avr);
			return mvarView is not null;
		}

		private static bool TryElapsedForRemainingMeters(
			Circulation circulation,
			long remainingMeters,
			out TimeSpan elapsed,
			out long routePk)
		{
			IReadOnlyList<TimedCall> calls = circulation.Calls;
			int last = calls.Count - 1;
			elapsed = TimeSpan.Zero;
			routePk = calls[0].Pk;
			if (last < 1)
				return false;

			long destPk = CabinItinerary.DestinationRoutePk(circulation) ?? calls[last].Pk;
			long originPk = CabinItinerary.OriginRoutePk(circulation) ?? calls[0].Pk;
			long trip = Math.Abs(destPk - originPk);
			long remain = remainingMeters < 0 ? 0 : remainingMeters;
			if (remain > trip)
				remain = trip;

			int sense = destPk >= originPk ? 1 : -1;
			long targetPk = destPk - sense * remain;
			if (targetPk == destPk && trip > 0)
				targetPk = destPk - sense;

			double clock = 0;
			int i = 0;
			while (i < last)
			{
				TimedCall here = calls[i];
				TimedCall next = calls[i + 1];
				clock += SimDwellSeconds;
				long a = here.Pk;
				long b = next.Pk;
				long lo = Math.Min(a, b);
				long hi = Math.Max(a, b);
				bool onHop = targetPk >= lo && targetPk <= hi;
				double run = ScheduledRun(here, next).TotalSeconds;
				if (!onHop)
				{
					clock += run;
					i++;
					continue;
				}

				double span = b - a;
				double u = Math.Abs(span) < 1.0 ? 1.0 : (targetPk - a) / span;
				if (u < 0) u = 0;
				if (u > 1) u = 1;
				if (u == 0 && i < last)
					u = 0.02;

				elapsed = TimeSpan.FromSeconds(clock + u * run);
				routePk = a + (long)Math.Round(u * span);
				return true;
			}

			elapsed = SimTripDuration(circulation) - TimeSpan.FromSeconds(SimDwellSeconds);
			if (elapsed < TimeSpan.Zero)
				elapsed = TimeSpan.Zero;
			routePk = destPk;
			return true;
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
			mvarTourmaline.SessionConfig.SimulatedSpeed = 0;
			SetStatus(status);
		}

		/// <summary>
		/// Deja de inyectar velocidad simulada. El dummy MVB residual se tira
		/// para que el bus real vuelva a rellenar (si no, CurrentSpeed seguiría
		/// leyendo el último valor de demo).
		/// </summary>
		private static void RestoreLiveTelemetry(SessionConfiguration session)
		{
			session.SimulatedSpeed = 0;
			session.CurrentNeutralSpeed = 0;
			if (!session.ServiceMode.MVBDummy)
				session.CurrentMVBData = null;
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
