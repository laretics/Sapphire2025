namespace Diamond.Basis
{
	/// <summary>
	/// Entidad lineal sobre un eje genérico: inicio (<see cref="Punctual{T,TAxis}.PK"/>) y longitud.
	/// </summary>
	public abstract class Lineal<T, TAxis> : Punctual<T, TAxis>
		where T : struct
		where TAxis : IAxisAlgebra<T>
	{
		private T mvarLength;

		protected Lineal()
			: base()
		{
			mvarLength = TAxis.Zero;
		}

		protected Lineal(T pk, T length)
			: base(pk)
		{
			mvarLength = length;
		}

		/// <summary>
		/// Longitud del tramo en unidades del eje (puede ser negativa hasta aplicar <see cref="Normalize"/>).
		/// </summary>
		public T Length
		{
			get { return mvarLength; }
			set { mvarLength = value; }
		}

		/// <summary>
		/// Extremo final (<c>PK + Length</c>).
		/// Al asignarlo se mantiene el inicio y se recalcula <see cref="Length"/>.
		/// </summary>
		public T PKEnd
		{
			get { return TAxis.Add(PK, mvarLength); }
			set { mvarLength = TAxis.Subtract(value, PK); }
		}

		/// <summary>
		/// Si la longitud es negativa, invierte el tramo: el inicio pasa a ser el antiguo final
		/// y la longitud queda en valor absoluto.
		/// </summary>
		public void Normalize()
		{
			if (TAxis.IsNegative(mvarLength))
			{
				PK = TAxis.Add(PK, mvarLength);
				mvarLength = TAxis.Negate(mvarLength);
			}
		}

		/// <summary>
		/// Formato [inicio]-[fin] según el álgebra del eje.
		/// </summary>
		public override string ToString()
		{
			return $"{TAxis.Format(PK)}-{TAxis.Format(PKEnd)}";
		}
	}
}
