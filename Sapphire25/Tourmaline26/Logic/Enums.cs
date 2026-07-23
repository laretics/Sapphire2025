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
        /// Las pantallas TFT y los paneles de todo el tren deben dar la misma información en el
        /// mismo momento. Para asegurarnos de ello, tendremos una variable en SessionConfiguration
        /// de este tipo enumerado.
        /// </summary>
        public enum PassengerInformationMode : byte
        {
            Default=0,          //Mostramos el destino y la información de viaje resumida.
            BeginOfTrip=1,      //Trayecto no iniciado. Información antes de la salida.
            NextStopsList=2,    //Lista de las próximas estaciones.
            NextStopInfo=3,     //Correspondencias e información de la próxima estación.
            Cruise=4,           //Tren viajando a velocidad de crucero
            EndOfTrip=5         //Trayecto finalizado. Tren en destino

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
