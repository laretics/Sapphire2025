using System;
using System.Collections.Generic;
using System.Text;

namespace Diamond.Basis
{
	/// <summary>
	/// Conjunto de tramos <see cref="Lineal{T,TAxis}"/> sobre un eje, con intersección vacía entre ellos.
	/// Los tramos se mantienen ordenados por PK, normalizados (longitud no negativa) y fusionados
	/// si se solapan o se tocan por el extremo.
	/// Estructura clásica: lista ordenada de intervalos disjuntos (disjoint interval set).
	/// </summary>
	public abstract class VectorFlex<T, TAxis>
		where T : struct
		where TAxis : IAxisAlgebra<T>
	{
		private readonly List<Lineal<T, TAxis>> mcolLineals;

		protected VectorFlex()
		{
			mcolLineals = new List<Lineal<T, TAxis>>();
		}

		/// <summary>
		/// Tramos actuales (ordenados por PK, disjuntos y adyacentes ya fusionados).
		/// No mutar PK/Length de estos elementos: se rompería el invariante.
		/// </summary>
		public IReadOnlyList<Lineal<T, TAxis>> Lineals
		{
			get { return mcolLineals; }
		}

		public int Count
		{
			get { return mcolLineals.Count; }
		}

		/// <summary>
		/// Fábrica de tramos al fusionar o partir. Las subclases deciden el tipo concreto de lineal.
		/// </summary>
		protected abstract Lineal<T, TAxis> CreateLineal(T pk, T length);

		/// <summary>
		/// Une el tramo al conjunto. Si solapa o toca tramos existentes, se fusionan en uno solo.
		/// </summary>
		public void Add(Lineal<T, TAxis> segment)
		{
			if (segment is null)
			{
				throw new ArgumentNullException(nameof(segment));
			}

			T start;
			T end;
			GetNormalizedRange(segment, out start, out end);

			if (TAxis.Compare(start, end) == 0)
			{
				return;
			}

			int index = 0;
			while (index < mcolLineals.Count && TAxis.Compare(GetExclusiveEnd(mcolLineals[index]), start) < 0)
			{
				index++;
			}

			T mergedStart = start;
			T mergedEnd = end;
			int removeCount = 0;
			int mergeIndex = index;

			while (mergeIndex < mcolLineals.Count && TAxis.Compare(mcolLineals[mergeIndex].PK, mergedEnd) <= 0)
			{
				T pieceStart = mcolLineals[mergeIndex].PK;
				T pieceEnd = GetExclusiveEnd(mcolLineals[mergeIndex]);

				if (TAxis.Compare(pieceStart, mergedStart) < 0)
				{
					mergedStart = pieceStart;
				}

				if (TAxis.Compare(pieceEnd, mergedEnd) > 0)
				{
					mergedEnd = pieceEnd;
				}

				removeCount++;
				mergeIndex++;
			}

			if (removeCount > 0)
			{
				mcolLineals.RemoveRange(index, removeCount);
			}

			mcolLineals.Insert(index, CreateLineal(mergedStart, TAxis.Subtract(mergedEnd, mergedStart)));
		}

		/// <summary>
		/// Resta el tramo del conjunto. Puede eliminar tramos, recortarlos o partirlos en dos.
		/// </summary>
		public void Subtract(Lineal<T, TAxis> segment)
		{
			if (segment is null)
			{
				throw new ArgumentNullException(nameof(segment));
			}

			T start;
			T end;
			GetNormalizedRange(segment, out start, out end);

			if (TAxis.Compare(start, end) == 0)
			{
				return;
			}

			int index = 0;
			while (index < mcolLineals.Count)
			{
				T pieceStart = mcolLineals[index].PK;
				T pieceEnd = GetExclusiveEnd(mcolLineals[index]);

				// Completamente a la izquierda del tramo restado.
				if (TAxis.Compare(pieceEnd, start) <= 0)
				{
					index++;
					continue;
				}

				// Completamente a la derecha: el resto de la lista también lo estará.
				if (TAxis.Compare(pieceStart, end) >= 0)
				{
					break;
				}

				// Hay solape: sustituir la pieza por los restos (0, 1 o 2 tramos).
				mcolLineals.RemoveAt(index);

				if (TAxis.Compare(pieceStart, start) < 0)
				{
					mcolLineals.Insert(index, CreateLineal(pieceStart, TAxis.Subtract(start, pieceStart)));
					index++;
				}

				if (TAxis.Compare(pieceEnd, end) > 0)
				{
					mcolLineals.Insert(index, CreateLineal(end, TAxis.Subtract(pieceEnd, end)));
					index++;
				}
			}
		}

		/// <summary>
		/// Unión de cero o más conjuntos. El resultado se escribe en <paramref name="result"/>
		/// (se vacía antes). Es seguro que <paramref name="result"/> figure también entre los operandos.
		/// </summary>
		public static void Union(VectorFlex<T, TAxis> result, params VectorFlex<T, TAxis>[] operands)
		{
			if (result is null)
			{
				throw new ArgumentNullException(nameof(result));
			}

			if (operands is null)
			{
				throw new ArgumentNullException(nameof(operands));
			}

			List<Lineal<T, TAxis>> snapshot = new List<Lineal<T, TAxis>>();
			int operandIndex = 0;
			while (operandIndex < operands.Length)
			{
				VectorFlex<T, TAxis> operand = operands[operandIndex];
				if (operand is null)
				{
					throw new ArgumentNullException(nameof(operands), "Un operando de Union es null.");
				}

				int linealIndex = 0;
				while (linealIndex < operand.mcolLineals.Count)
				{
					snapshot.Add(operand.mcolLineals[linealIndex]);
					linealIndex++;
				}

				operandIndex++;
			}

			result.Clear();

			int addIndex = 0;
			while (addIndex < snapshot.Count)
			{
				result.Add(snapshot[addIndex]);
				addIndex++;
			}
		}

		/// <summary>
		/// Intersección de uno o más conjuntos. El resultado se escribe en <paramref name="result"/>
		/// (se vacía antes). Es seguro que <paramref name="result"/> figure también entre los operandos.
		/// Sin operandos, el resultado queda vacío.
		/// </summary>
		public static void Intersection(VectorFlex<T, TAxis> result, params VectorFlex<T, TAxis>[] operands)
		{
			if (result is null)
			{
				throw new ArgumentNullException(nameof(result));
			}

			if (operands is null)
			{
				throw new ArgumentNullException(nameof(operands));
			}

			if (operands.Length == 0)
			{
				result.Clear();
				return;
			}

			int operandIndex = 0;
			while (operandIndex < operands.Length)
			{
				if (operands[operandIndex] is null)
				{
					throw new ArgumentNullException(nameof(operands), "Un operando de Intersection es null.");
				}

				operandIndex++;
			}

			// Copia de rangos del primer operando (snapshot por si result == operands[0]).
			List<AxisRange> current = SnapshotRanges(operands[0]);

			operandIndex = 1;
			while (operandIndex < operands.Length)
			{
				if (current.Count == 0)
				{
					break;
				}

				List<AxisRange> next = SnapshotRanges(operands[operandIndex]);
				current = IntersectRangeLists(current, next);
				operandIndex++;
			}

			result.ReplaceWithRanges(current);
		}

		/// <summary>
		/// Diferencia (complemento relativo): <c>minuend − subtrahend1 − subtrahend2 − …</c>.
		/// El resultado se escribe en <paramref name="result"/> (se vacía antes).
		/// Es seguro que <paramref name="result"/> figure entre los operandos.
		/// Sin sustraendos, el resultado es una copia de <paramref name="minuend"/>.
		/// </summary>
		public static void Difference(
			VectorFlex<T, TAxis> result,
			VectorFlex<T, TAxis> minuend,
			params VectorFlex<T, TAxis>[] subtrahends)
		{
			if (result is null)
			{
				throw new ArgumentNullException(nameof(result));
			}

			if (minuend is null)
			{
				throw new ArgumentNullException(nameof(minuend));
			}

			if (subtrahends is null)
			{
				throw new ArgumentNullException(nameof(subtrahends));
			}

			int subIndex = 0;
			while (subIndex < subtrahends.Length)
			{
				if (subtrahends[subIndex] is null)
				{
					throw new ArgumentNullException(nameof(subtrahends), "Un sustraendo de Difference es null.");
				}

				subIndex++;
			}

			List<AxisRange> current = SnapshotRanges(minuend);

			subIndex = 0;
			while (subIndex < subtrahends.Length)
			{
				if (current.Count == 0)
				{
					break;
				}

				List<AxisRange> toRemove = SnapshotRanges(subtrahends[subIndex]);
				current = SubtractRangeLists(current, toRemove);
				subIndex++;
			}

			result.ReplaceWithRanges(current);
		}

		/// <summary>
		/// Puntos frontera: inicio y fin de cada tramo normalizado contenido en este conjunto,
		/// ordenados a lo largo del eje.
		/// </summary>
		public IReadOnlyList<Punctual<T, TAxis>> Frontiers()
		{
			List<Punctual<T, TAxis>> mcolFrontiers = new List<Punctual<T, TAxis>>(mcolLineals.Count * 2);

			int index = 0;
			while (index < mcolLineals.Count)
			{
				Lineal<T, TAxis> segment = mcolLineals[index];
				mcolFrontiers.Add(new AxisPoint(segment.PK));
				mcolFrontiers.Add(new AxisPoint(segment.PKEnd));
				index++;
			}

			return mcolFrontiers;
		}

		public void Clear()
		{
			mcolLineals.Clear();
		}

		/// <summary>
		/// Indica si el valor de eje está cubierto por algún tramo.
		/// Semántica semiabierta: cubre [PK, PKEnd).
		/// </summary>
		public bool Contains(T value)
		{
			int index = 0;
			while (index < mcolLineals.Count)
			{
				T pieceStart = mcolLineals[index].PK;
				T pieceEnd = GetExclusiveEnd(mcolLineals[index]);

				if (TAxis.Compare(value, pieceStart) < 0)
				{
					return false;
				}

				if (TAxis.Compare(value, pieceEnd) < 0)
				{
					return true;
				}

				index++;
			}

			return false;
		}

		public override string ToString()
		{
			if (mcolLineals.Count == 0)
			{
				return "∅";
			}

			StringBuilder builder = new StringBuilder();
			int index = 0;
			while (index < mcolLineals.Count)
			{
				if (index > 0)
				{
					builder.Append(" ∪ ");
				}

				builder.Append(mcolLineals[index].ToString());
				index++;
			}

			return builder.ToString();
		}

		private void ReplaceWithRanges(List<AxisRange> ranges)
		{
			mcolLineals.Clear();

			int index = 0;
			while (index < ranges.Count)
			{
				AxisRange range = ranges[index];
				mcolLineals.Add(CreateLineal(range.Start, TAxis.Subtract(range.End, range.Start)));
				index++;
			}
		}

		private static List<AxisRange> SnapshotRanges(VectorFlex<T, TAxis> source)
		{
			List<AxisRange> ranges = new List<AxisRange>(source.mcolLineals.Count);
			int index = 0;
			while (index < source.mcolLineals.Count)
			{
				Lineal<T, TAxis> segment = source.mcolLineals[index];
				ranges.Add(new AxisRange(segment.PK, GetExclusiveEnd(segment)));
				index++;
			}

			return ranges;
		}

		/// <summary>
		/// Intersección de dos listas de intervalos disjuntos ordenados [start, end).
		/// </summary>
		private static List<AxisRange> IntersectRangeLists(List<AxisRange> left, List<AxisRange> right)
		{
			List<AxisRange> output = new List<AxisRange>();
			int i = 0;
			int j = 0;

			while (i < left.Count && j < right.Count)
			{
				AxisRange a = left[i];
				AxisRange b = right[j];

				// a completamente a la izquierda de b (sin solape; contacto extremo no cuenta).
				if (TAxis.Compare(a.End, b.Start) <= 0)
				{
					i++;
					continue;
				}

				// b completamente a la izquierda de a.
				if (TAxis.Compare(b.End, a.Start) <= 0)
				{
					j++;
					continue;
				}

				T overlapStart = Max(a.Start, b.Start);
				T overlapEnd = Min(a.End, b.End);

				if (TAxis.Compare(overlapStart, overlapEnd) < 0)
				{
					output.Add(new AxisRange(overlapStart, overlapEnd));
				}

				// Avanza el intervalo que termina antes (o ambos si terminan igual).
				if (TAxis.Compare(a.End, b.End) < 0)
				{
					i++;
				}
				else if (TAxis.Compare(b.End, a.End) < 0)
				{
					j++;
				}
				else
				{
					i++;
					j++;
				}
			}

			return output;
		}

		/// <summary>
		/// Diferencia de dos listas de intervalos disjuntos ordenados: <c>left − right</c>.
		/// </summary>
		private static List<AxisRange> SubtractRangeLists(List<AxisRange> left, List<AxisRange> right)
		{
			if (left.Count == 0 || right.Count == 0)
			{
				return left;
			}

			List<AxisRange> output = new List<AxisRange>();
			int i = 0;
			int j = 0;

			T curStart = left[0].Start;
			T curEnd = left[0].End;

			while (i < left.Count)
			{
				// Sin más sustraendos: emitir el tramo actual y el resto de left intacto.
				if (j >= right.Count)
				{
					if (TAxis.Compare(curStart, curEnd) < 0)
					{
						output.Add(new AxisRange(curStart, curEnd));
					}

					i++;
					while (i < left.Count)
					{
						output.Add(left[i]);
						i++;
					}

					break;
				}

				AxisRange sub = right[j];

				// Sustraendo completamente a la izquierda del tramo actual.
				if (TAxis.Compare(sub.End, curStart) <= 0)
				{
					j++;
					continue;
				}

				// Sustraendo completamente a la derecha: el tramo actual sobrevive entero.
				if (TAxis.Compare(sub.Start, curEnd) >= 0)
				{
					if (TAxis.Compare(curStart, curEnd) < 0)
					{
						output.Add(new AxisRange(curStart, curEnd));
					}

					i++;
					if (i < left.Count)
					{
						curStart = left[i].Start;
						curEnd = left[i].End;
					}

					continue;
				}

				// Hay solape: trozo izquierdo que queda.
				if (TAxis.Compare(curStart, sub.Start) < 0)
				{
					output.Add(new AxisRange(curStart, sub.Start));
				}

				// Avanza el inicio del tramo actual al final del solape con este sustraendo.
				if (TAxis.Compare(sub.End, curEnd) < 0)
				{
					curStart = sub.End;
					j++;
				}
				else
				{
					// El sustraendo se come hasta el final (o más) del tramo actual.
					i++;
					if (i < left.Count)
					{
						curStart = left[i].Start;
						curEnd = left[i].End;
					}
				}
			}

			return output;
		}

		private static T Min(T left, T right)
		{
			if (TAxis.Compare(left, right) <= 0)
			{
				return left;
			}

			return right;
		}

		private static T Max(T left, T right)
		{
			if (TAxis.Compare(left, right) >= 0)
			{
				return left;
			}

			return right;
		}

		/// <summary>
		/// Rango normalizado [start, end) a partir de un lineal (no muta el original).
		/// </summary>
		private static void GetNormalizedRange(Lineal<T, TAxis> segment, out T start, out T end)
		{
			T pk = segment.PK;
			T length = segment.Length;

			if (TAxis.IsNegative(length))
			{
				start = TAxis.Add(pk, length);
				end = pk;
			}
			else
			{
				start = pk;
				end = TAxis.Add(pk, length);
			}
		}

		private static T GetExclusiveEnd(Lineal<T, TAxis> segment)
		{
			return TAxis.Add(segment.PK, segment.Length);
		}

		private readonly struct AxisRange
		{
			public AxisRange(T start, T end)
			{
				Start = start;
				End = end;
			}

			public T Start { get; }

			public T End { get; }
		}

		/// <summary>
		/// Punto de frontera ligero; solo geometría de eje.
		/// </summary>
		private sealed class AxisPoint : Punctual<T, TAxis>
		{
			public AxisPoint(T pk)
				: base(pk)
			{
			}
		}
	}
}
