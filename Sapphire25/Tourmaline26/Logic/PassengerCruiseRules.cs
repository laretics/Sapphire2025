namespace Tourmaline26.Logic
{
	/// <summary>
	/// Cuándo el TFT muestra la lista de próximas estaciones en lugar del
	/// crucero (vídeo + mapa). El cartel de correspondencias
	/// (<see cref="Enums.PassengerInformationMode.NextStopInfo"/>) tiene
	/// prioridad cuando ya arranca el anuncio de llegada.
	/// </summary>
	internal static class PassengerCruiseRules
	{
		public const int NextStopsMaxSpeedKmh = 20;
		public const int NextStopsHoldMeters = 1000;

		public static bool ShowNextStopsList(int speedKmh, long remainingMetersToNextStation)
		{
			if (speedKmh < NextStopsMaxSpeedKmh)
				return true;
			return remainingMetersToNextStation < NextStopsHoldMeters;
		}
	}
}
