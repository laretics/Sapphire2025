using System.Text;

namespace Tourmaline26.Components.Services.Logic
{
	public static class Enums
	{

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
