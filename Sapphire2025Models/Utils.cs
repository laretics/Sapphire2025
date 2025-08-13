using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2025Models
{
	/// <summary>
	/// Librería de funciones de utilidad para llamar en cualquier parte del programa
	/// </summary>
	public class Utils
	{
		public static bool hasRole(byte Credentials, Common.UserRole role)
		{
			byte auxRole = (byte)role;
			return getBit(Credentials, auxRole);
		}
		public static bool getBit(byte rhs, byte byteId)
		{
			return (rhs & (1 << byteId)) != 0;
		}
		public static byte setBit(byte rhs, byte byteId)
		{
			return (byte)(rhs | (1 << byteId));
		}

		/// <summary>
		/// Traduce un intervalo en texto
		/// </summary>
		/// <param name="rhs">Intervalo</param>
		/// <returns></returns>
		public static string autoInterval(TimeSpan rhs, bool timeFormat)
		{
			if(timeFormat)
				return string.Format("{0:00}:{1:00}", rhs.Hours, rhs.Minutes);
			else
			{
				StringBuilder salida = new StringBuilder();
				if(rhs.Hours>0)
				{
					if (rhs.Hours == 1)
						salida.Append("una hora");
					else
						salida.AppendFormat("{0} h",rhs.Hours);
				}
				if(rhs.Minutes>0)
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
						salida.AppendFormat("{0} min",rhs.Minutes);
				}
				if(rhs.Seconds>0)
				{
					if (salida.Length > 0)
						salida.Append(" y ");
					if (rhs.Seconds == 1)
						salida.Append("un segundo");
					else
						salida.AppendFormat("{0} s",rhs.Seconds);
				}
				return salida.ToString();
			}
		}
		

		public static string autoDate(DateTime rhs)
		{
			DateTime ahora = DateTime.Now;
			double dias = ahora.Subtract(rhs).TotalDays;
			if(rhs.Equals(DateTime.MinValue))
			{
				return "-";
			}
			else
			{
				if (dias < 1)
				{
					return string.Format("{0:HH:mm}", rhs);
				}
				else if (dias < 2)
				{
					return string.Format("Ayer {0:HH:mm}", rhs);
				}
				else
				{
					return string.Format("{0:dd-MM-yy}", rhs);
				}
			}
		}
		public static string TrainStyleFill(string? trainId)
		{
			if (null != trainId)
			{
				if (trainId.Length > 0)
				{
					switch (trainId.ToUpper()[0])
					{
						case '1':
							return "#f2f2ff";
						case '6':
							return "#fff2ff";
						case '7':
							return "#fffff2";
						case '8':
							return "#fff2f2";
						case '9':
							return "#fff2ff";
						default:
							return "#f2f2f2";
					}
				}
			}
			return "transparent";
		}

		/// <summary>
		/// Auxiliar para definir la compatibilidad de un turno con un día concreto de la semana
		/// </summary>
		/// <param name="pattern">Patrón del turno</param>
		/// <param name="today">Tipo enumerado DateTime con el día de hoy</param>
		/// <param name="todayIsFestive">Flag que indica si hoy se considera festivo según el calendario laboral</param>
		/// <returns></returns>
		public static bool IsDayCompatible(byte pattern, DayOfWeek today, bool todayIsFestive)
		{
			if (todayIsFestive)
			{
				if (getBit(pattern, 7)) return true; //Da igual lo demás... es un turno festivo.
				if (!getBit(pattern, 0) && !getBit(pattern, 6)) return false; //Si es festivo y el turno no es de sábado ni de domingo NO concuerda.
			}				
			return (pattern & (1 << (byte)today))!=0;
		}

	}
}
