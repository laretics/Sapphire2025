using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimeNet2026.Timed
{
    public class TimeLapse
    {
        public TimeSpan Begin { get; set; }
        public TimeSpan End { get; set; }
        public TimeSpan Duration => End.Subtract(Begin);
        public bool Contains(TimeSpan rhs)
        {
            return (rhs >= Begin && rhs <= End);
        }
        /// <summary>
        /// Amplía la duración de este intervalo en la cantidad pasada (por la derecha y por la izquierda)
        /// </summary>
        /// <param name="amount">Cantidad de tiempo que se infla</param>
        /// <returns>El intervalo inflado</returns>
        public TimeLapse Inflate(TimeSpan amount)
        {
            TimeSpan mitad = new TimeSpan(amount.Ticks / 2);
            TimeLapse salida = new TimeLapse();
            salida.Begin = this.Begin.Subtract(mitad);
            salida.End = this.End.Add(mitad);
            return salida;
        }
        public TimeLapse(TimeSpan begin, TimeSpan end)
        {
            Begin = begin;
            End = end;
		}
		public TimeLapse(string? begin, string? end)
		{
            if(null==begin || null==end)
            {
                this.Begin = new TimeSpan(0);
                this.End = new TimeSpan(0);
			}
            else
            {
				TimeSpan? tsBegin = parseSapphireTimeSpan(begin);
				TimeSpan? tsEnd = parseSapphireTimeSpan(end);
				if (null == tsBegin || null == tsEnd)
					throw new ArgumentException("Invalid time span format");
				Begin = tsBegin.Value;
				End = tsEnd.Value;
			}               
		}
        public TimeLapse() : this(TimeSpan.Zero, TimeSpan.Zero) { }

		private TimeSpan? parseSapphireTimeSpan(string? rhs)
		{
			if (null == rhs) return null;
			TimeSpan salida;
			if (TimeSpan.TryParseExact(rhs, new[] { "hh\\:mm", "h\\:mm" }, System.Globalization.CultureInfo.InvariantCulture, out salida))
				return salida;
			return null;
		}
	}
}
