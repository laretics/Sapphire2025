using Diamond.Cabin;
using Diamond.Project;

namespace Tourmaline26.Logic
{
	/// <summary>
	/// Interior y exterior siguen el mismo
	/// <see cref="Enums.PassengerInformationMode"/> que los TFT,
	/// con una traducción distinta por cara.
	/// </summary>
	public static class PassengerLedMapping
	{
		public static Enums.PassengerLedKind Resolve(SessionConfiguration session)
		{
			if (!session.MainSwitches.TeleindicatorsEnabled)
				return Enums.PassengerLedKind.Blank;
			if (session.InformationLevel == Enums.InformationLevel.Forbidden)
				return Enums.PassengerLedKind.OutOfService;
			if (!session.MainSwitches.PASEnabled)
				return Enums.PassengerLedKind.Blank;

			CabinEnvironment? cabin = session.Cabin;
			Circulation? circulation = cabin?.Circulation;
			if (circulation is null || cabin?.Asimilation is null)
				return Enums.PassengerLedKind.ClockWeatherSpeed;

			if (session.InformationLevel != Enums.InformationLevel.Route)
				return Enums.PassengerLedKind.ClockWeatherSpeed;

			return session.InformationMode switch
			{
				Enums.PassengerInformationMode.BeginOfTrip =>
					Enums.PassengerLedKind.DestinationAndCar,
				Enums.PassengerInformationMode.NextStopsList =>
					Enums.PassengerLedKind.ClockWeatherSpeed,
				Enums.PassengerInformationMode.Cruise =>
					Enums.PassengerLedKind.ClockWeatherSpeed,
				Enums.PassengerInformationMode.NextStopInfo
					or Enums.PassengerInformationMode.EndOfTrip =>
					IsStoppedAtDestination(session)
						? Enums.PassengerLedKind.DestinationAndCar
						: Enums.PassengerLedKind.NextStation,
				_ => Enums.PassengerLedKind.DestinationAndCar
			};
		}

		/// <summary>
		/// Exterior: destino en bienvenida, cartel de correspondencias
		/// o tren parado en una estación. En el resto, número de tren.
		/// </summary>
		public static Enums.PassengerLedExteriorKind ResolveExterior(SessionConfiguration session)
		{
			if (!session.MainSwitches.TeleindicatorsEnabled)
				return Enums.PassengerLedExteriorKind.Blank;
			if (session.InformationLevel == Enums.InformationLevel.Forbidden)
				return Enums.PassengerLedExteriorKind.OutOfService;
			if (!session.MainSwitches.PASEnabled)
				return Enums.PassengerLedExteriorKind.Blank;

			CabinEnvironment? cabin = session.Cabin;
			Circulation? circulation = cabin?.Circulation;
			if (circulation is null || cabin?.Asimilation is null)
				return Enums.PassengerLedExteriorKind.TrainNumber;

			if (session.InformationLevel != Enums.InformationLevel.Route)
				return Enums.PassengerLedExteriorKind.TrainNumber;

			if (!session.MainSwitches.ExternalTeleindicatorsEnabled)
				return Enums.PassengerLedExteriorKind.TrainNumber;

			bool showDestination = session.InformationMode
					is Enums.PassengerInformationMode.BeginOfTrip
					or Enums.PassengerInformationMode.NextStopInfo
					or Enums.PassengerInformationMode.EndOfTrip
				|| IsStoppedAtStation(session);

			if (showDestination && !string.IsNullOrWhiteSpace(DestinationName(session)))
				return Enums.PassengerLedExteriorKind.Destination;

			return Enums.PassengerLedExteriorKind.TrainNumber;
		}

		/// <summary>
		/// Texto del anuncio prefijado para el LED, o vacío si no hay que mostrarlo.
		/// Media: interior. Alta: interior y exterior.
		/// </summary>
		public static bool TryLedAnnouncement(
			SessionConfiguration session,
			out string text,
			out bool interior,
			out bool exterior)
		{
			text = string.Empty;
			interior = false;
			exterior = false;

			if (!session.PassengerAnnouncementEnabled)
				return false;

			PassengerInformation? info = session.PassengerAnnouncement;
			if (info is null || !info.IsVisible)
				return false;

			interior = info.ShowsOnInteriorLed;
			exterior = info.ShowsOnExteriorLed;
			if (!interior && !exterior)
				return false;

			text = (info.MessageText ?? string.Empty).Replace("|", "   ").Trim();
			if (text.Length == 0)
				text = (info.CurrentText ?? string.Empty).Trim();
			return text.Length > 0;
		}

		/// <summary>
		/// Cartel de llegada en TFT, pero el tren ya está parado en el destino:
		/// el LED interior anuncia destino y coche, no la próxima estación.
		/// </summary>
		public static bool IsStoppedAtDestination(SessionConfiguration session)
		{
			CabinEnvironment? cabin = session.Cabin;
			StationInfo? dest = cabin?.Asimilation?.Destination;
			StationInfo? current = cabin?.CurrentStation;
			if (cabin is null || dest is null || current is null)
				return false;
			if (!string.Equals(current.Id, dest.Id, StringComparison.Ordinal))
				return false;
			return session.CurrentSpeed <= 0;
		}

		/// <summary>Tren detenido en el área de una estación.</summary>
		public static bool IsStoppedAtStation(SessionConfiguration session)
		{
			CabinEnvironment? cabin = session.Cabin;
			if (cabin?.CurrentStation is null)
				return false;
			return session.CurrentSpeed <= 0;
		}

		/// <summary>Misma estación que el cartel TFT de correspondencias.</summary>
		public static StationInfo? NextStation(SessionConfiguration session)
		{
			if (session.PreviewArrivalStation is not null)
				return session.PreviewArrivalStation;

			CabinEnvironment? cabin = session.Cabin;
			if (cabin?.CurrentStation is not null)
				return cabin.CurrentStation;

			IReadOnlyList<TimedCall>? remaining = cabin?.RemainingCalls;
			if (remaining is not null && remaining.Count > 0)
				return remaining[0].Station;

			return cabin?.Asimilation?.Destination;
		}

		public static StationInfo? DestinationStation(SessionConfiguration session)
		{
			return session.Cabin?.Asimilation?.Destination
				?? session.Cabin?.Circulation?.Asimilation?.Destination;
		}

		public static string NextStationName(SessionConfiguration session) =>
			NextStation(session)?.Name ?? string.Empty;

		public static string DestinationName(SessionConfiguration session) =>
			DestinationStation(session)?.Name ?? string.Empty;

		public static string TrainNumber(SessionConfiguration session)
		{
			Circulation? circulation = session.Cabin?.Circulation;
			if (circulation is null)
				return string.Empty;
			return circulation.HasServiceNumber ? circulation.ServiceNumber : circulation.Id;
		}
	}
}
