using System;

namespace Diamond.Basis
{
	/// <summary>
	/// Álgebra de un eje 1D (posición y desplazamiento del mismo tipo T).
	/// Implementar en un <c>struct</c> para que el JIT especialice e inline las operaciones.
	/// </summary>
	public interface IAxisAlgebra<T> where T : struct
	{
		static abstract T Zero { get; }

		static abstract int Compare(T left, T right);

		static abstract T Add(T left, T right);

		static abstract T Subtract(T left, T right);

		static abstract T Negate(T value);

		static abstract bool IsNegative(T value);

		static abstract bool IsZero(T value);

		/// <summary>
		/// Representación legible de un valor del eje (PK ferroviario, hora, etc.).
		/// </summary>
		static abstract string Format(T value);
	}

	/// <summary>
	/// Eje espacial en metros (PK). Formato [km+mmm].
	/// </summary>
	public readonly struct LongAxis : IAxisAlgebra<long>
	{
		public static long Zero
		{
			get { return 0L; }
		}

		public static int Compare(long left, long right)
		{
			return left.CompareTo(right);
		}

		public static long Add(long left, long right)
		{
			return left + right;
		}

		public static long Subtract(long left, long right)
		{
			return left - right;
		}

		public static long Negate(long value)
		{
			return -value;
		}

		public static bool IsNegative(long value)
		{
			return value < 0L;
		}

		public static bool IsZero(long value)
		{
			return value == 0L;
		}

		public static string Format(long value)
		{
			long absPk = Math.Abs(value);
			long km = absPk / 1000L;
			long meters = absPk % 1000L;

			if (value < 0L)
			{
				return $"[-{km}+{meters:D3}]";
			}

			return $"[{km}+{meters:D3}]";
		}
	}

	/// <summary>
	/// Eje temporal con <see cref="TimeSpan"/> (desplazamiento respecto al origen del esquema).
	/// </summary>
	public readonly struct TimeSpanAxis : IAxisAlgebra<TimeSpan>
	{
		public static TimeSpan Zero
		{
			get { return TimeSpan.Zero; }
		}

		public static int Compare(TimeSpan left, TimeSpan right)
		{
			return left.CompareTo(right);
		}

		public static TimeSpan Add(TimeSpan left, TimeSpan right)
		{
			return left + right;
		}

		public static TimeSpan Subtract(TimeSpan left, TimeSpan right)
		{
			return left - right;
		}

		public static TimeSpan Negate(TimeSpan value)
		{
			return -value;
		}

		public static bool IsNegative(TimeSpan value)
		{
			return value < TimeSpan.Zero;
		}

		public static bool IsZero(TimeSpan value)
		{
			return value == TimeSpan.Zero;
		}

		public static string Format(TimeSpan value)
		{
			// Formato constante, compatible con negativos (p. ej. -1.02:03:04.0000000).
			return $"[{value.ToString("c")}]";
		}
	}
}
