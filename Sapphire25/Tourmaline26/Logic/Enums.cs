using System.Text;

namespace Tourmaline26.Logic
{
	public static class Enums
	{
        public enum DeviceType
        {
            HMI,
            TFT,
            Led,
            Server3D
        }
        public enum CoachEnum
        {
            Undeterminated = 0,
            M1 = 1,
            M2 = 2,
            M3 = 3,
            M4 = 4,
            N1 = 1,
            N2 = 2,
            N3 = 3,
            N4 = 4,
            R1 = 1,
            R2 = 2,
            R3 = 3,
            R4 = 4,
        }
        public enum Orientation
        {
            Neutral = 0,
            Forward = 1,
            Backward = 2
        }
        public enum CameraType:byte
		{
			None=0,
			Inside=1,
			Frontal=2,
			Outside=3,
			Pantograph=4,
			Mirror=5,
			Other=255			
		}
		public enum CameraCodec:byte
		{
			None=0,
			R2P=1,
			other=255
		}
		public enum TrainSeries:byte
		{
			None=0,
			S6100=1,
			S7100=2,
			S8100=3,
			S9100=4,
			S1100=5,
			ManFGC=6,
			Other=255
		}

		public enum InformationLevel:byte
		{
			Disabled=0, //Anunciador al viajero desconectada
			Route=1, //Anunciador al viajero en modo normal
			Forbidden=2 //Tren no admite viajeros
		}
        /// <summary>
        /// Las pantallas TFT y los paneles LED de todo el tren siguen el mismo modo
        /// (<see cref="SessionConfiguration.InformationMode"/>). El LED no tiene
        /// estado propio: <see cref="PassengerLedMapping"/> traduce cada valor.
        /// </summary>
        public enum PassengerInformationMode : byte
        {
            Default = 0,        // TFT: imagen de tren. LED: destino y coche.
            BeginOfTrip = 1,    // TFT: bienvenida. LED int: destino y coche. LED ext: destino.
            NextStopsList = 2,  // TFT: lista de próximas estaciones. LED int: hora/temp/vel. LED ext: número de tren.
            NextStopInfo = 3,   // TFT: cartel de correspondencias. LED int: próxima estación (destino y coche si parado en destino). LED ext: destino.
            Cruise = 4,         // TFT: stream Experience + mapa. LED int: hora/temp/vel. LED ext: número de tren (destino si parado en estación).
            EndOfTrip = 5       // TFT: mismo cartel de llegada. LED int: próxima estación, o destino y coche si parado en destino. LED ext: destino.
        }

        /// <summary>
        /// Contenido del teleindicador interior, derivado de
        /// <see cref="PassengerInformationMode"/>.
        /// </summary>
        public enum PassengerLedKind : byte
        {
            Blank = 0,
            OutOfService = 1,
            DestinationAndCar = 2,
            ClockWeatherSpeed = 3,
            NextStation = 4
        }

        /// <summary>
        /// Contenido del teleindicador exterior: destino (bienvenida,
        /// correspondencias o parado en estación) o número de tren.
        /// </summary>
        public enum PassengerLedExteriorKind : byte
        {
            Blank = 0,
            OutOfService = 1,
            Destination = 2,
            TrainNumber = 3
        }

        /// <summary>
        /// Traduce un intervalo en texto
        /// </summary>
        /// <param name="rhs">Intervalo</param>
        /// <returns></returns>
        public static string autoInterval(TimeSpan rhs, bool timeFormat, bool addSeconds = false)
        {
            if (timeFormat)
                return string.Format("{0:00}:{1:00}", rhs.Hours, rhs.Minutes);
            else
            {
                StringBuilder salida = new StringBuilder();
                if (rhs.Hours > 0)
                {
                    if (rhs.Hours == 1)
                        salida.Append("una hora");
                    else
                        salida.AppendFormat("{0} h", rhs.Hours);
                }
                if (rhs.Minutes > 0)
                {
                    if (salida.Length > 0)
                    {
                        if (rhs.Seconds > 0)
                            salida.Append(" , ");
                        else
                            salida.Append(" y ");
                    }

                    if (rhs.Minutes == 1)
                        salida.Append("un minuto");
                    else
                        salida.AppendFormat("{0} min", rhs.Minutes);
                }
                if (rhs.Seconds > 0 && addSeconds)
                {
                    if (salida.Length > 0)
                        salida.Append(" y ");
                    if (rhs.Seconds == 1)
                        salida.Append("un segundo");
                    else
                        salida.AppendFormat("{0} s", rhs.Seconds);
                }
                return salida.ToString();
            }
        }

    }
}
