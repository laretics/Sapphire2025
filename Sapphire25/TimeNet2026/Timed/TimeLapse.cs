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
    }
}
