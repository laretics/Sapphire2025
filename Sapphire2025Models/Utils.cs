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

	}
}
