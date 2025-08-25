using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
		public static string autoInterval(TimeSpan rhs, bool timeFormat, bool addSeconds = false)
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
				if(rhs.Seconds>0 && addSeconds)
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

		/// <summary>
		/// Dada una circulación, obtiene el número de orden de la misma buscando el número en la cadena
		/// </summary>
		/// <param name="circulationId">Nombre completo de la circulación</param>
		/// <returns>Número de la circulación</returns>
		public static int ExtractCirculationNumber(string circulationId)
		{
            if (string.IsNullOrWhiteSpace(circulationId))
                return -1;

            // Busca el primer grupo de dígitos en la cadena
            var match = System.Text.RegularExpressions.Regex.Match(circulationId, @"\d+");
            if (match.Success)
            {
                if (int.TryParse(match.Value, out int result))
                    return result;
            }
            return -1;
        }

		/// <summary>
		/// Dado el número de una circulación, obtiene la paridad. Even es par. Odd impar.
		/// </summary>
		/// <param name="circulationId">Número de la circulación en formato int</param>
		/// <returns>True si es par o nulo</returns>
		public static bool IsEven(int circulationId)
		{
			return circulationId % 2 == 0;
		}

		public enum SFMIniteraryAsimilation:byte
		{
			Unknown=0,
			Material=1,
			Type42=2,
			Marratxi=3,
			ManacorFestive=4,
			Inca=5,
			SaPoblaTram=6,
			SaPobla=7,
			SaPoblaFestive=8,
			Manacor=9,
			ParcBit=10,
			ParcBitFestive=11,
			Other=255
		}

		public static SFMIniteraryAsimilation GetAsimilation(string? circulationId)
		{
			if (null!= circulationId)
			{
                string rhs = circulationId.ToUpper();
                int auxNumero = ExtractCirculationNumber(rhs);
                if (auxNumero > 3999)
                {
                    if (rhs.StartsWith("40") || rhs.StartsWith("41") || rhs.StartsWith("70")) return SFMIniteraryAsimilation.Material;
                    if (rhs.StartsWith("42")) return SFMIniteraryAsimilation.Type42;
                    if (rhs.StartsWith("43")) return SFMIniteraryAsimilation.Marratxi;
                    if (rhs.StartsWith("44")) return SFMIniteraryAsimilation.ManacorFestive;
                    if (rhs.StartsWith("45")) return SFMIniteraryAsimilation.Inca;
                    if (rhs.StartsWith("46")) return SFMIniteraryAsimilation.SaPoblaTram;
                    if (rhs.StartsWith("47")) return SFMIniteraryAsimilation.SaPobla;
                    if (rhs.StartsWith("48")) return SFMIniteraryAsimilation.SaPoblaFestive;
                    if (rhs.StartsWith("49")) return SFMIniteraryAsimilation.Manacor;
                    if (rhs.StartsWith("50")) return SFMIniteraryAsimilation.ParcBit;
                    if (rhs.StartsWith("51") || rhs.StartsWith("55")) return SFMIniteraryAsimilation.ParcBitFestive;
                }
                else
                {
                    if (rhs.Contains("SP")) return SFMIniteraryAsimilation.SaPobla;
                    if (rhs.Contains("I")) return SFMIniteraryAsimilation.Inca;
                    if (rhs.Contains("MT")) return SFMIniteraryAsimilation.Marratxi;
                    if (rhs.Contains("M")) return SFMIniteraryAsimilation.Manacor;
                    if (rhs.Contains("UI")) return SFMIniteraryAsimilation.ParcBit;
                    if (rhs.Contains("PB")) return SFMIniteraryAsimilation.ParcBit;
                }
            }
            return SFMIniteraryAsimilation.Unknown;
		}

		public static string AsimilationColor(SFMIniteraryAsimilation rhs)
		{
			switch(rhs)
			{
				case SFMIniteraryAsimilation.Unknown: return "#C387F0";
                case SFMIniteraryAsimilation.Material: return "#E6D7E0";

                case SFMIniteraryAsimilation.Marratxi: return "#FF9999";

                case SFMIniteraryAsimilation.Inca: return "#C0DBA8";

                case SFMIniteraryAsimilation.SaPobla: return "#ABD9F2";
                case SFMIniteraryAsimilation.SaPoblaTram: return "#B4E5FF";
                case SFMIniteraryAsimilation.SaPoblaFestive: return "#9FC9E0";

                case SFMIniteraryAsimilation.Manacor: return "#FFE781";
                case SFMIniteraryAsimilation.ManacorFestive: return "#EDD778";

                case SFMIniteraryAsimilation.ParcBit: return "#FFCC99";
                case SFMIniteraryAsimilation.ParcBitFestive: return "#E3B688";

                case SFMIniteraryAsimilation.Type42: return "#BFF0E1";                
                           
                default: return "transparent";
            }			
		}

	}
}
