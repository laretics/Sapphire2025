using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using TimeNet2026.Auxiliar;
using TimeNet2026.Storage;

namespace TimeNet2026.Topo
{
	public class Lineal : Punctual
	{
		internal Lineal()
		{
			pk = 0;
			length = 0;
			Tracks = 255;
		}
		internal Lineal(XmlNode root)
		{
			this.pk = XMLUtil.LongParam(root, "pk0");
			this.pkEnd = XMLUtil.LongParam(root, "pkf");
			this.Tracks = XMLUtil.ByteParam(root, "par");
		}

		internal byte Tracks { get; set; } //Vías en las que está este elemento definido. (Flags binarios)
		internal virtual long length { get; set; }
		internal long pkEnd
		{
			get => pk + length;
			set => length = value - pk;
		}
		internal virtual bool contains(Punctual rhs)
		{
			return (rhs.pk > pk) && (rhs.pk < pkEnd);
		}
		internal override long distanceFrom(long pk)
		{
			long auxDistance1, auxDistance2;
			if (pk > this.pk && pk < pkEnd) return 0;
			auxDistance1 = base.distanceFrom(pk);
			auxDistance2 = (long)Math.Abs(this.pkEnd - pk);
			return (auxDistance1 < auxDistance2) ? auxDistance1 : auxDistance2;
		}
		public override string ToString()
		{
			return string.Format("{0}-{1}+{2:000}", base.ToString(), (pk + length) / 1000, (pk + length) % 1000);

		}

		internal override bool tryParse(string rhs)
		{
			//Formato x+xxx-y+yyyy
			Punctual punto0, punto1;
			punto0 = new Punctual();
			if (rhs.Contains('-'))
			{
				string[] auxTexto = rhs.Split('-');
				if (auxTexto.Length > 1)
				{
					if (punto0.tryParse(auxTexto[0]))
					{
						punto1 = new Punctual();
						if (punto1.tryParse(auxTexto[1]))
						{
							if (punto0.pk < punto1.pk)
							{
								this.pk = punto0.pk;
								this.length = punto1.pk - punto0.pk;
							}
							else
							{
								this.pk = punto1.pk;
								this.length = punto0.pk - punto1.pk;
							}
							return true;
						}
					}
				}
			}
			else //Entidad lineal de longitud cero
			{
				if (punto0.tryParse(rhs))
				{
					this.pk = punto0.pk;
					this.length = 0;
					return true;
				}
			}
			return false;
		}
		internal static new List<OnyxField> Descriptor()
		{
			List<OnyxField> salida = Punctual.Descriptor();
			salida.Add(new OnyxField("length", "INTEGER"));
			return salida;
		}
	}
}
