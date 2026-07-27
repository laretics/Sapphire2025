using System;
using System.Text;

namespace Diamond.Timed
{
	/// <summary>
	/// Días de la semana en que aplica un requisito (flags).
	/// Atajos: <see cref="Laborables"/> (lun–vie, ~freq lab) y <see cref="Festivos"/> (sáb–dom, aproximación a freq fes).
	/// </summary>
	[Flags]
	public enum ServiceDay
	{
		None = 0,
		Monday = 1 << 0,
		Tuesday = 1 << 1,
		Wednesday = 1 << 2,
		Thursday = 1 << 3,
		Friday = 1 << 4,
		Saturday = 1 << 5,
		Sunday = 1 << 6,

		/// <summary>Lunes a viernes (laborables tipo SFM lab).</summary>
		Laborables = Monday | Tuesday | Wednesday | Thursday | Friday,

		/// <summary>Sábado y domingo (aproximación a festivos; festivos entre semana se modelan aparte si hace falta).</summary>
		Festivos = Saturday | Sunday,

		/// <summary>Todos los días de la semana.</summary>
		All = Laborables | Festivos
	}

	/// <summary>
	/// Conjunto de días de circulación de un requisito, con helpers de parseo y consulta.
	/// </summary>
	public sealed class ServiceDays
	{
		private ServiceDay mvarDays;

		public ServiceDays(ServiceDay days)
		{
			mvarDays = days == ServiceDay.None ? ServiceDay.All : days;
		}

		public static ServiceDays All
		{
			get { return new ServiceDays(ServiceDay.All); }
		}

		public static ServiceDays Laborables
		{
			get { return new ServiceDays(ServiceDay.Laborables); }
		}

		public static ServiceDays Festivos
		{
			get { return new ServiceDays(ServiceDay.Festivos); }
		}

		public ServiceDay Days
		{
			get { return mvarDays; }
			set { mvarDays = value == ServiceDay.None ? ServiceDay.All : value; }
		}

		public bool AppliesOn(DayOfWeek dayOfWeek)
		{
			return (mvarDays & FromDayOfWeek(dayOfWeek)) != ServiceDay.None;
		}

		public bool AppliesOn(ServiceDay day)
		{
			return (mvarDays & day) != ServiceDay.None;
		}

		public static ServiceDay FromDayOfWeek(DayOfWeek day)
		{
			switch (day)
			{
				case DayOfWeek.Monday:
					return ServiceDay.Monday;
				case DayOfWeek.Tuesday:
					return ServiceDay.Tuesday;
				case DayOfWeek.Wednesday:
					return ServiceDay.Wednesday;
				case DayOfWeek.Thursday:
					return ServiceDay.Thursday;
				case DayOfWeek.Friday:
					return ServiceDay.Friday;
				case DayOfWeek.Saturday:
					return ServiceDay.Saturday;
				case DayOfWeek.Sunday:
					return ServiceDay.Sunday;
				default:
					return ServiceDay.None;
			}
		}

		public static string FormatDayOfWeek(DayOfWeek day)
		{
			switch (day)
			{
				case DayOfWeek.Monday:
					return "lun";
				case DayOfWeek.Tuesday:
					return "mar";
				case DayOfWeek.Wednesday:
					return "mié";
				case DayOfWeek.Thursday:
					return "jue";
				case DayOfWeek.Friday:
					return "vie";
				case DayOfWeek.Saturday:
					return "sáb";
				case DayOfWeek.Sunday:
					return "dom";
				default:
					return day.ToString();
			}
		}

		/// <summary>
		/// Parsea tokens de días: lab, fes, all, mon/lun, mon-fri, lun-vie, etc.
		/// </summary>
		public static bool TryParse(System.Collections.Generic.IReadOnlyList<string> tokens, int startIndex, out ServiceDays days, out int consumed, out string? error)
		{
			days = All;
			consumed = 0;
			error = null;

			if (tokens is null || startIndex >= tokens.Count)
			{
				error = "falta la especificación de días.";
				return false;
			}

			ServiceDay mask = ServiceDay.None;
			int index = startIndex;

			while (index < tokens.Count)
			{
				string raw = tokens[index];
				string t = raw.ToLowerInvariant();

				// Rango mon-fri / lun-vie
				int dash = t.IndexOf('-');
				if (dash > 0 && dash < t.Length - 1)
				{
					ServiceDay from;
					ServiceDay to;
					if (!TryParseOneDay(t.Substring(0, dash), out from) || !TryParseOneDay(t.Substring(dash + 1), out to))
					{
						error = "rango de días no válido '" + raw + "'.";
						return false;
					}

					mask |= ExpandRange(from, to);
					index++;
					continue;
				}

				if (t == "lab" || t == "laborables" || t == "weekday" || t == "weekdays")
				{
					mask |= ServiceDay.Laborables;
					index++;
					continue;
				}

				if (t == "fes" || t == "festivos" || t == "weekend" || t == "we")
				{
					mask |= ServiceDay.Festivos;
					index++;
					continue;
				}

				if (t == "all" || t == "todos" || t == "daily")
				{
					mask = ServiceDay.All;
					index++;
					// all cierra la lista
					break;
				}

				ServiceDay one;
				if (TryParseOneDay(t, out one))
				{
					mask |= one;
					index++;
					continue;
				}

				// Token que no es día → fin de la lista de días
				break;
			}

			consumed = index - startIndex;
			if (consumed == 0)
			{
				error = "no se reconoció ningún día en '" + tokens[startIndex] + "'.";
				return false;
			}

			if (mask == ServiceDay.None)
			{
				error = "el conjunto de días quedó vacío.";
				return false;
			}

			days = new ServiceDays(mask);
			return true;
		}

		private static bool TryParseOneDay(string t, out ServiceDay day)
		{
			day = ServiceDay.None;
			switch (t)
			{
				case "mon":
				case "monday":
				case "lun":
				case "lunes":
					day = ServiceDay.Monday;
					return true;
				case "tue":
				case "tuesday":
				case "mar":
				case "martes":
					day = ServiceDay.Tuesday;
					return true;
				case "wed":
				case "wednesday":
				case "mie":
				case "mié":
				case "miercoles":
				case "miércoles":
					day = ServiceDay.Wednesday;
					return true;
				case "thu":
				case "thursday":
				case "jue":
				case "jueves":
					day = ServiceDay.Thursday;
					return true;
				case "fri":
				case "friday":
				case "vie":
				case "viernes":
					day = ServiceDay.Friday;
					return true;
				case "sat":
				case "saturday":
				case "sab":
				case "sáb":
				case "sabado":
				case "sábado":
					day = ServiceDay.Saturday;
					return true;
				case "sun":
				case "sunday":
				case "dom":
				case "domingo":
					day = ServiceDay.Sunday;
					return true;
				default:
					return false;
			}
		}

		private static ServiceDay ExpandRange(ServiceDay from, ServiceDay to)
		{
			// Orden natural de bits 0..6
			int fromBit = BitIndex(from);
			int toBit = BitIndex(to);
			if (fromBit < 0 || toBit < 0)
			{
				return from | to;
			}

			if (fromBit > toBit)
			{
				int swap = fromBit;
				fromBit = toBit;
				toBit = swap;
			}

			ServiceDay mask = ServiceDay.None;
			int b = fromBit;
			while (b <= toBit)
			{
				mask |= (ServiceDay)(1 << b);
				b++;
			}

			return mask;
		}

		private static int BitIndex(ServiceDay day)
		{
			int v = (int)day;
			if (v == 0 || (v & (v - 1)) != 0)
			{
				// no es un único bit
				return -1;
			}

			int i = 0;
			while ((v & 1) == 0)
			{
				v >>= 1;
				i++;
			}

			return i;
		}

		public override string ToString()
		{
			if (mvarDays == ServiceDay.All)
			{
				return "all";
			}

			if (mvarDays == ServiceDay.Laborables)
			{
				return "lab";
			}

			if (mvarDays == ServiceDay.Festivos)
			{
				return "fes";
			}

			StringBuilder sb = new StringBuilder();
			AppendIf(sb, ServiceDay.Monday, "lun");
			AppendIf(sb, ServiceDay.Tuesday, "mar");
			AppendIf(sb, ServiceDay.Wednesday, "mié");
			AppendIf(sb, ServiceDay.Thursday, "jue");
			AppendIf(sb, ServiceDay.Friday, "vie");
			AppendIf(sb, ServiceDay.Saturday, "sáb");
			AppendIf(sb, ServiceDay.Sunday, "dom");
			return sb.ToString();
		}

		private void AppendIf(StringBuilder sb, ServiceDay flag, string label)
		{
			if ((mvarDays & flag) == 0)
			{
				return;
			}

			if (sb.Length > 0)
			{
				sb.Append(' ');
			}

			sb.Append(label);
		}
	}
}
