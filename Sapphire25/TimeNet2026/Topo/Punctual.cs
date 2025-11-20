using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimeNet2026.Topo
{
	internal class Punctual
	{
		const long NEAR_FACTOR = 150;
		internal virtual long pk { get; set; }
		internal bool isNear(long pk)
		{
			return Math.Abs(pk - this.pk) < NEAR_FACTOR;
		}
		internal virtual long distanceFrom(long pk)
		{
			return (long)System.Math.Abs(pk - this.pk);
		}
		internal virtual bool tryParse(string rhs)
		{
			//Posibles entradas:
			//xx+xxx
			//xxx
			//xx,xxx
			//xx-xxx
			string auxCadena = rhs.Trim();
			auxCadena = auxCadena.Replace('+', '|');
			auxCadena = auxCadena.Replace(',', '|');
			string[] auxArray = auxCadena.Split('|');
			int km, meters;
			meters = 0;
			if (int.TryParse(auxArray[0], out km))
			{
				if (auxArray.Length > 1)
				{
					if (!int.TryParse(auxArray[1], out meters)) return false;
				}
				this.pk = km * 1000 + (long)meters;
				return true;
			}
			return false;
		}
		public override string ToString()
		{
			return string.Format("{0}+{1:000}", pk / 1000, pk % 1000);
		}
	}
}
