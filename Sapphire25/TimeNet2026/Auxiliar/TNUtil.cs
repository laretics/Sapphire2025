using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimeNet2026.Auxiliar
{
	internal static class TNUtil
	{
		internal static Weekday parseWeekDays(string? rhs)
		{
			if (null == rhs)
				return Weekday.All; //Cualquier día de la semana.
			else
			{
				string cadenaWeek = rhs.Trim().ToUpper();
				if (cadenaWeek.Equals("FFF"))
					return Weekday.AllFestives; //Sábados domingos y festivos.
				else if (cadenaWeek.Equals("LAB"))
					return Weekday.Labour; //Lunes a viernes.
				else
				{
					Weekday salida = 0;
					if (cadenaWeek.Contains('L')) salida |= Weekday.Monday;
					if (cadenaWeek.Contains('M')) salida |= Weekday.Tuesday;
					if (cadenaWeek.Contains('X')) salida |= Weekday.Wednesday;
					if (cadenaWeek.Contains('J')) salida |= Weekday.Tuesday;
					if (cadenaWeek.Contains('V')) salida |= Weekday.Friday;
					if (cadenaWeek.Contains('S')) salida |= Weekday.Saturday;
					if (cadenaWeek.Contains('D')) salida |= Weekday.Sunday;
					if (cadenaWeek.Contains('F')) salida |= Weekday.Festive;
					return salida;
				}
			}
		}
	}
}
