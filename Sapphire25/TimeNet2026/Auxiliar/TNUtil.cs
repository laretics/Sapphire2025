using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimeNet2026.Auxiliar
{
	internal static class TNUtil
	{
		internal static byte parseWeekDays(string? rhs)
		{
			if (null == rhs)
				return 0xff; //Cualquier día de la semana.
			else
			{
				string cadenaWeek = rhs.Trim().ToUpper();
				if (cadenaWeek.Equals("FFF"))
					return 1 | 64 | 128; //Sábados domingos y festivos.
				else if (cadenaWeek.Equals("LAB"))
					return 2 | 4 | 8 | 16 | 32; //Laborables.
				else
				{
					byte salida = 0;
					if (cadenaWeek.Contains('L')) salida |= 2;
					if (cadenaWeek.Contains('M')) salida |= 4;
					if (cadenaWeek.Contains('X')) salida |= 8;
					if (cadenaWeek.Contains('J')) salida |= 16;
					if (cadenaWeek.Contains('V')) salida |= 32;
					if (cadenaWeek.Contains('S')) salida |= 64;
					if (cadenaWeek.Contains('D')) salida |= 1;
					if (cadenaWeek.Contains('F')) salida |= 128;
					return salida;
				}
			}
		}
	}
}
