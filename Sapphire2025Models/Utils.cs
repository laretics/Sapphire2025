using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
	}
}
