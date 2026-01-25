using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace TimeNet2026.Timed
{
    public class TimeLapseCollection
    {
        internal List<TimeLapse> mcolLapse;

        public TimeLapseCollection()
        {
            mcolLapse = new List<TimeLapse>();
        }
        public IEnumerable<TimeLapse> Intervals => mcolLapse;
        /// <summary>
        /// Devuelve el comienzo del primer intervalo
        /// </summary>
        public TimeSpan Begin
        {
            get
            {
                if (mcolLapse.Count < 1) return new TimeSpan(1,0,0,0);
                return mcolLapse[0].Begin;
            }
        }
        public TimeSpan End
        {
            get
            {
                if (mcolLapse.Count < 1) return new TimeSpan(0);
                return mcolLapse.Last().Begin;
            }
        }
        public TimeLapse? Envolvent ()
        {
            if(mcolLapse.Count<1) return null;
            return new TimeLapse { Begin = this.Begin, End = this.End };
		}
        public void Add(TimeLapse rhs)
        {
            mcolLapse.Add(rhs);
            MergeIntervals();
        }
        public void Remove (TimeLapse rhs)
        {
            List<TimeLapse> nueva = new List<TimeLapse>();
            foreach(TimeLapse interval in mcolLapse)
            {
                if (interval.End <= rhs.Begin || interval.Begin >= rhs.End)
                    nueva.Add(interval); //No hay solapamiento
                else
                {
                    //Tenemos solapamiento.
                    if(interval.Begin<rhs.Begin)
                        nueva.Add(new TimeLapse { Begin = interval.Begin, End = rhs.Begin });
                    if (interval.End > rhs.End)
                        nueva.Add(new TimeLapse { Begin = rhs.End, End = interval.End });
                }
            }
            mcolLapse = nueva;
        }
        public static TimeLapseCollection Union(params TimeLapseCollection[] collections)
        {
            var result = new TimeLapseCollection();
            foreach (var col in collections)
            {
                foreach (var interval in col.mcolLapse)
					result.Add(interval);
            }
            result.MergeIntervals();
            return result;
        }
        public static TimeLapseCollection Union(params TimeLapse[] intervals)
        {
            var result = new TimeLapseCollection();
            foreach (var interval in intervals)
				result.Add(interval);
			result.MergeIntervals();
			return result;
        }

        public static TimeLapseCollection Interseccion(params TimeLapseCollection[] collections)
        {
            if (collections.Length == 0) return new TimeLapseCollection();
            TimeLapseCollection salida = new TimeLapseCollection();
            salida.mcolLapse.AddRange(collections[0].mcolLapse); // Copy first
            for (int k = 1; k < collections.Length; k++)
                salida = salida.Intersection(collections[k]);
			salida.MergeIntervals();
			return salida;
        }

        private TimeLapseCollection Intersection(TimeLapseCollection other)
        {
            TimeLapseCollection salida = new TimeLapseCollection();
            int i = 0, j = 0;
            while(i<mcolLapse.Count && j<other.mcolLapse.Count)
            {
                TimeLapse intA = this.mcolLapse[i];
                TimeLapse intB = other.mcolLapse[j];
                TimeSpan start = TimeSpan.FromTicks(Math.Max(intA.Begin.Ticks, intB.Begin.Ticks));
                TimeSpan end = TimeSpan.FromTicks(Math.Min(intA.End.Ticks, intB.End.Ticks));
                if (start < end)
                    salida.Add(new TimeLapse { Begin = start, End = end });
                if (intA.End <= intB.End)
                    i++;
                else
                    j++;
            }
            salida.MergeIntervals();
            return salida;
        }
        
        /// <summary>
        /// Infla (o desinfla) los elementos de esta colección en el importe que se suministra
        /// Si los intervalos inflados se solapan, la cantidad de elementos del resultado puede ser menor que la del origen.
        /// </summary>
        /// <param name="amount">Cantidad de tiempo que se infla a los intervalos</param>
        /// <returns>La colección inflada</returns>
        public TimeLapseCollection Inflate(TimeSpan amount)
        {
            TimeLapseCollection salida = new TimeLapseCollection();
            foreach (TimeLapse lapso in mcolLapse)
                salida.Add(lapso.Inflate(amount));
            salida.MergeIntervals();
            return salida;
        }
        /// <summary>
        /// Devuelve la colección invertida.
        /// </summary>
        /// <returns>Colección invertida de intervalos</returns>
        public TimeLapseCollection Inverse
        {
            get
            {
                TimeLapseCollection result = new TimeLapseCollection();
                TimeSpan totalEnd = TimeSpan.FromDays(1);
                TimeSpan currentStart = TimeSpan.Zero;
                foreach (var interval in mcolLapse)
                {
                    if (currentStart < interval.Begin)
                    {
                        result.Add(new TimeLapse { Begin = currentStart, End = interval.Begin });
                    }
                    currentStart = interval.End;
                }
                if (currentStart < totalEnd)
                {
                    result.Add(new TimeLapse { Begin = currentStart, End = totalEnd });
                }
                return result;
            }
        }

        public TimeSpan Duration
        {
            get
            {
                TimeSpan salida = new TimeSpan(0);
                foreach (TimeLapse intervalo in mcolLapse)
                    salida = salida.Add(intervalo.Duration);
                return salida;
            }
        }
        //Devuelve el tiempo que ha pasado de este intervalo desde el comienzo.
        //Suma todos los intervalos parciales hasta esta hora.
        public TimeSpan FromBegin(TimeSpan rhs)
        {
            TimeSpan salida = new TimeSpan(0);
            if (this.Begin > rhs)
                return salida; //En caso de que comience después de rhs, el tiempo será cero.

            foreach (TimeLapse intervalo in mcolLapse)
            {
                if (intervalo.Contains(rhs))
                    return salida.Add(rhs.Subtract(intervalo.Begin));

                salida = salida.Add(intervalo.Duration);            
            }
            return salida; //Si el intervalo está fuera del rango, devuelve todo.
        }

        public TimeSpan ToEnd(TimeSpan rhs)
        {
            TimeSpan salida = new TimeSpan(0);
            if (this.End < rhs)
                return salida; //En caso de que comience antes del final, el tiempo será cero.

            bool contando = false;
            foreach (TimeLapse intervalo in mcolLapse)
            {
                if (contando)
                    salida = salida.Add(intervalo.Duration);
                else
                {
                    if (intervalo.Contains(rhs))
                    {
                        salida = salida.Add(intervalo.End.Subtract(rhs));
                        contando = true;
                    }
                    else if (intervalo.End < rhs)
                    {
                        salida = salida.Add(intervalo.Duration);
                        contando = true;
                    }                      
                }    
            }
            return salida;
        }

        /// <summary>
        /// Intervalo de mayor duración en la colección.
        /// </summary>
        public TimeLapse? Maximal
        {
            get
            {
                if (mcolLapse.Count < 1) return null;
                TimeLapse candidato = mcolLapse[0];
                foreach (TimeLapse elemento in mcolLapse)
                {
                    if (elemento.Duration > candidato.Duration)
                        candidato = elemento;
                }
                return candidato;
            }
        }

        /// <summary>
        /// Intervalo de menor duración en la colección.
        /// </summary>        
        public TimeLapse? Minimal
        {
            get
            {
                if (mcolLapse.Count < 1) return null;
                TimeLapse candidato = mcolLapse[0];
                foreach (TimeLapse elemento in mcolLapse)
                {
                    if (elemento.Duration < candidato.Duration)
                        candidato = elemento;
                }
                return candidato;
            }
        }
        /// <summary>
        /// Obtiene los momentos en los que hay transición.
        /// </summary>
        public IEnumerable<TimeSpan> Frontiers
        {
            get
            {
                List<TimeSpan> salida = new List<TimeSpan>();
                foreach(TimeLapse lapso in mcolLapse)
                {
                    salida.Add(lapso.Begin);
                    salida.Add(lapso.End);
                }
                return salida;
            }
        }

        private void MergeIntervals()
        {
            if (mcolLapse.Count <= 1) return;
            mcolLapse.Sort((a, b) => a.Begin.CompareTo(b.Begin));
            List<TimeLapse> merged = new List<TimeLapse>();
            TimeLapse current = mcolLapse[0];
            for (int i = 1; i < mcolLapse.Count; i++)
            {
                if (current.End >= mcolLapse[i].Begin)
                {
                    // Solapamiento o adyacencia: fusionar
                    current.End = TimeSpan.FromTicks(Math.Max(current.End.Ticks, mcolLapse[i].End.Ticks));
                }
                else
                {
                    merged.Add(current);
                    current = mcolLapse[i];
                }
            }
            merged.Add(current);
            mcolLapse = merged;
        }

    }
}
