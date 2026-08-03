using BlazorBootstrap;
using Sapphire2025Models.Authentication;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
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

		public static bool isRoot (SessionModel? session)
		{
			if(null!=session)
				return session.Roles.Contains(Common.UserRole.Root);
			return false;
		}
		public static bool isEngineer(SessionModel? session)
		{
			if (isRoot(session)) return true;
			if (null != session)
				return session.Roles.Contains(Common.UserRole.Engineer);
			return false;
		}
		public static bool isExpert(SessionModel?session)
		{
			if (isRoot(session)) return true;
			if (null != session)
				return session.Roles.Contains(Common.UserRole.Expert);
			return false;
		}
		public static bool isInspector(SessionModel? session)
		{
			if (isRoot(session)) return true;
			if (null != session)
				return session.Roles.Contains(Common.UserRole.Inspector);
			return false;
		}
		public static bool isOfficial(SessionModel? session)
		{
			if (isEngineer(session)) return true;
			if (null != session)
				return session.Roles.Contains(Common.UserRole.Oficial);
			return false;
		}
		public static bool isMechanic(SessionModel? session)
		{
			if (isOfficial(session)) return true;
			if (null != session)
				return session.Roles.Contains(Common.UserRole.Mechanic);
			return false;
		}
		public static bool isStation(SessionModel? session)
		{
			if (isInspector(session)) return true;
			if (null != session)
				return session.Roles.Contains(Common.UserRole.Station);
			return false;
		}
		public static bool isAnonymous(SessionModel? session)
		{
			if(isMechanic(session)||isStation(session)) return true;
			if (null != session)
				return session.Roles.Contains(Common.UserRole.Anonymous);
			return false;
		}

		public static bool CanOpenTask(SessionModel? session)
		{
			return isMechanic(session);
		}
		public static bool CanCloseTask(SessionModel? session)
		{
			return isOfficial(session);
		}
		public static bool CanVerifyTask(SessionModel? session)
		{
			return isInspector(session);
		}
		public static bool OrderTypeIsWash(Guid orderType)
		{
			return orderType == Common.WorkOrderTypeManualWash ||
			orderType == Common.WorkOrderTypePlatformWash ||
			orderType == Common.WorkOrderTypeTunnelWash;
		}

		public static string AtenuateColor(string rhs)
		{
			if (string.IsNullOrWhiteSpace(rhs) || !rhs.StartsWith("#") || rhs.Length != 7)
				return rhs; //No atenuamos

			int r = Convert.ToInt32(rhs.Substring(1, 2), 16) / 2;
			int g = Convert.ToInt32(rhs.Substring(3, 2), 16) / 2;
			int b = Convert.ToInt32(rhs.Substring(5, 2), 16) / 2;

			return $"#{r:X2}{g:X2}{b:X2}";
		}

		/// <summary>
		/// Estilo CSS de celda/badge de turno: fondo = BgColor, texto = Color.
		/// Si falta Color, o el contraste texto/fondo es insuficiente (p. ej. colores
		/// invertidos o col ausente en el XML), se elige blanco o negro según la
		/// luminancia del fondo para que el número de turno siga siendo legible.
		/// </summary>
		public static string WorkShiftTemplateStyle(
			Expert.WorkshiftTemplates.WorkShiftTemplateModel? template,
			bool isTd = false,
			string missingTemplateStyle = "background: #dc3545; color: #fff;")
		{
			if (null == template)
				return missingTemplateStyle;

			string bg = NormalizeCssColor(template.BgColor) ?? "transparent";
			string? color = NormalizeCssColor(template.Color);
			if (string.IsNullOrEmpty(color) || !HasReadableContrast(color, bg))
				color = ContrastingTextColor(bg);
			if (isTd)
				color = "#00CC00";

			return $"background: {bg}; color: {color};";
		}

		/// <summary>
		/// Color de franja inferior del turno (StripeColor).
		/// </summary>
		public static string WorkShiftTemplateBarStyle(
			Expert.WorkshiftTemplates.WorkShiftTemplateModel? template)
		{
			string bar = NormalizeCssColor(template?.StripeColor) ?? "transparent";
			return $"background: {bar};";
		}

		/// <summary>
		/// Devuelve blanco o negro según la luminancia del color de fondo.
		/// Fondos transparentes o no parseables → negro (comportamiento histórico).
		/// </summary>
		public static string ContrastingTextColor(string? backgroundColor)
		{
			if (!TryParseCssColor(backgroundColor, out int r, out int g, out int b))
				return "black";

			// Luminancia relativa (sRGB aproximada). Umbral ~0.45 favorece texto claro sobre grises medios-oscuros.
			double luminance = RelativeLuminance(r, g, b);
			return luminance < 0.45 ? "#ffffff" : "black";
		}

		/// <summary>
		/// Contraste suficiente entre texto y fondo (diferencia de luminancia).
		/// Si el fondo no se puede parsear (transparent), se asume legible.
		/// </summary>
		public static bool HasReadableContrast(string? foreground, string? background)
		{
			if (!TryParseCssColor(background, out int br, out int bg, out int bb))
				return true; // transparente / desconocido: no forzamos

			if (!TryParseCssColor(foreground, out int fr, out int fg, out int fb))
			{
				// Nombres CSS comunes
				if (string.Equals(foreground, "black", StringComparison.OrdinalIgnoreCase)
					|| string.Equals(foreground, "#000", StringComparison.OrdinalIgnoreCase)
					|| string.Equals(foreground, "#000000", StringComparison.OrdinalIgnoreCase))
				{
					fr = fg = fb = 0;
				}
				else if (string.Equals(foreground, "white", StringComparison.OrdinalIgnoreCase)
					|| string.Equals(foreground, "#fff", StringComparison.OrdinalIgnoreCase)
					|| string.Equals(foreground, "#ffffff", StringComparison.OrdinalIgnoreCase))
				{
					fr = fg = fb = 255;
				}
				else
					return false; // no parseable → tratar como ilegible y recalcular
			}

			double lf = RelativeLuminance(fr, fg, fb);
			double lb = RelativeLuminance(br, bg, bb);
			// Diferencia mínima ~0.35: negro (#000, L=0) sobre #555 (L≈0.27) fallaría; sobre #d3d3e8 pasa.
			return Math.Abs(lf - lb) >= 0.35;
		}

		private static double RelativeLuminance(int r, int g, int b)
			=> (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255.0;

		/// <summary>
		/// Normaliza un color CSS: trim y null si está vacío.
		/// </summary>
		public static string? NormalizeCssColor(string? value)
		{
			if (string.IsNullOrWhiteSpace(value))
				return null;
			return value.Trim();
		}

		/// <summary>
		/// Intenta parsear #RGB, #RRGGBB o #AARRGGBB (ignora alpha).
		/// </summary>
		public static bool TryParseCssColor(string? value, out int r, out int g, out int b)
		{
			r = g = b = 0;
			string? color = NormalizeCssColor(value);
			if (null == color || color.Equals("transparent", StringComparison.OrdinalIgnoreCase))
				return false;
			if (!color.StartsWith('#'))
				return false;

			string hex = color.Substring(1);
			try
			{
				if (hex.Length == 3)
				{
					r = Convert.ToInt32(new string(hex[0], 2), 16);
					g = Convert.ToInt32(new string(hex[1], 2), 16);
					b = Convert.ToInt32(new string(hex[2], 2), 16);
					return true;
				}
				if (hex.Length == 6)
				{
					r = Convert.ToInt32(hex.Substring(0, 2), 16);
					g = Convert.ToInt32(hex.Substring(2, 2), 16);
					b = Convert.ToInt32(hex.Substring(4, 2), 16);
					return true;
				}
				if (hex.Length == 8)
				{
					// AARRGGBB → ignoramos alpha
					r = Convert.ToInt32(hex.Substring(2, 2), 16);
					g = Convert.ToInt32(hex.Substring(4, 2), 16);
					b = Convert.ToInt32(hex.Substring(6, 2), 16);
					return true;
				}
			}
			catch
			{
				return false;
			}
			return false;
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
		public static string autoDate(DateTime? rhsh)
		{
			if (null == rhsh)
				return "-";
			
			DateTime rhs = (rhsh.HasValue ? rhsh.Value : DateTime.MinValue);
			// Si el DateTime no tiene Kind especificado, asumimos que es UTC
			if (rhs.Kind == DateTimeKind.Unspecified)
			{
				rhs = DateTime.SpecifyKind(rhs, DateTimeKind.Utc);
			}

			// Convertir a hora local
			DateTime localTime = rhs.Kind == DateTimeKind.Utc ? rhs.ToLocalTime() : rhs;
			DateTime ahora = DateTime.Now;

			if (localTime.Equals(DateTime.MinValue))
			{
				return "-";
			}
			else if (localTime.Date == ahora.Date)
			{
				// Mismo día de calendario
				return string.Format("{0:HH:mm}", localTime);
			}
			else if (localTime.Date == ahora.Date.AddDays(-1))
			{
				// Ayer
				return string.Format("Ayer {0:HH:mm}", localTime);
			}
			else
			{
				// Otro día
				return string.Format("{0:dd-MM-yy}", localTime);
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
					if (auxMatchAsimilation("40,41,70",rhs,true)) return SFMIniteraryAsimilation.Material;
					if (auxMatchAsimilation("42", rhs,true)) return SFMIniteraryAsimilation.Type42;
					if (auxMatchAsimilation("43,53", rhs,true)) return SFMIniteraryAsimilation.Marratxi;
					if (auxMatchAsimilation("44", rhs,true)) return SFMIniteraryAsimilation.ManacorFestive;
					if (auxMatchAsimilation("45", rhs,true)) return SFMIniteraryAsimilation.Inca;
					if (auxMatchAsimilation("46,67", rhs,true)) return SFMIniteraryAsimilation.SaPoblaTram;
					if (auxMatchAsimilation("47", rhs,true)) return SFMIniteraryAsimilation.SaPobla;
					if (auxMatchAsimilation("48", rhs,true)) return SFMIniteraryAsimilation.SaPoblaFestive;
					if (auxMatchAsimilation("49", rhs,true)) return SFMIniteraryAsimilation.Manacor;
					if (auxMatchAsimilation("50", rhs,true)) return SFMIniteraryAsimilation.ParcBit;
					if (auxMatchAsimilation("51,55", rhs,true)) return SFMIniteraryAsimilation.ParcBitFestive;
                }
                else
                {
                    if (rhs.Contains("SP")) return SFMIniteraryAsimilation.SaPobla;
					if (auxMatchAsimilation("INC,IN,I", rhs,false)) return SFMIniteraryAsimilation.Inca;
					if (auxMatchAsimilation("MTX,MT", rhs,false)) return SFMIniteraryAsimilation.Marratxi;
					if (auxMatchAsimilation("MAN,MNC,M", rhs,false)) return SFMIniteraryAsimilation.Manacor;
                    if (rhs.Contains("UI")) return SFMIniteraryAsimilation.ParcBit;
                    if (rhs.Contains("PB")) return SFMIniteraryAsimilation.ParcBit;
                }
            }
            return SFMIniteraryAsimilation.Unknown;
		}
		private static bool auxMatchAsimilation(string asimilationString, string rhs, bool start)
		{
			string[] matches = asimilationString.Split(',');
			foreach(string match in matches)
			{
				if (start && rhs.StartsWith(match)) return true;
				if (!start && rhs.Contains(match)) return true;
			}
			return false;
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

		public static string CheckGenerate(string order, DateTime expiry, Guid vehicleId)
		{
			string datePart = expiry.Date.ToString("yyyyMMdd");
			string input = $"{vehicleId:N}|{order}|{datePart}";

			byte[] hash;
			using (var sha = SHA256.Create())
				hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));

			string code = Base36Encode(hash.Take(5).ToArray()).PadLeft(5, '0').Substring(0, 5);
			char crc = CalcCRC(code);
			return code + crc;
		}

		public static bool CheckCheck(string code, string order, DateTime expiry, Guid vehicleId)
		{
			if (string.IsNullOrWhiteSpace(code) || code.Length != 6)
				return false;

			string codePart = code.Substring(0, 5);
			char crc = code[5];

			if (CalcCRC(codePart) != crc)
				return false;

			for (var date = expiry.Date; date >= DateTime.MinValue.Date; date = date.AddDays(-1))
			{
				string expected = CheckGenerate(order, date, vehicleId);
				if (string.Equals(code, expected, StringComparison.OrdinalIgnoreCase))
					return true;
			}
			return false;
		}

		// Base36: 0-9 + A-Z
		private static string Base36Encode(byte[] bytes)
		{
			const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
			var value = BitConverter.ToUInt64(bytes.Concat(new byte[8 - bytes.Length]).ToArray(), 0);
			var result = new StringBuilder();
			for (int i = 0; i < 6 && value > 0; i++)
			{
				result.Insert(0, chars[(int)(value % 36)]);
				value /= 36;
			}
			return result.ToString();
		}

		// CRC simple: XOR de todos los caracteres, convertido a base36
		private static char CalcCRC(string code)
		{
			int crc = 0;
			foreach (char c in code)
				crc ^= c;
			const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
			return chars[Math.Abs(crc) % 36];
		}
	}
}
