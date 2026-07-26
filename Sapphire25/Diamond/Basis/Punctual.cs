using System;

namespace Diamond.Basis
{
	/// <summary>
	/// Entidad puntual sobre un eje genérico. La posición se expresa como PK (distancia al origen).
	/// </summary>
	/// <typeparam name="T">Tipo escalar del eje (<see cref="long"/>, <see cref="TimeSpan"/>, ...).</typeparam>
	/// <typeparam name="TAxis">Álgebra estática del eje (p. ej. <see cref="LongAxis"/>, <see cref="TimeSpanAxis"/>).</typeparam>
	public abstract class Punctual<T, TAxis> : IComparable<Punctual<T, TAxis>>
		where T : struct
		where TAxis : IAxisAlgebra<T>
	{
		private T mvarPK;

		protected Punctual()
		{
			mvarPK = TAxis.Zero;
		}

		protected Punctual(T pk)
		{
			mvarPK = pk;
		}

		/// <summary>
		/// Distancia al punto 0 del eje (puede ser negativa según el álgebra).
		/// </summary>
		public T PK
		{
			get { return mvarPK; }
			set { mvarPK = value; }
		}

		public int CompareTo(Punctual<T, TAxis>? other)
		{
			if (other is null)
			{
				return 1;
			}

			return TAxis.Compare(mvarPK, other.mvarPK);
		}

		public override string ToString()
		{
			return TAxis.Format(mvarPK);
		}
	}
}
