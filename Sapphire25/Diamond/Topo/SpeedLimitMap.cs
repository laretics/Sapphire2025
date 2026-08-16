using System;
using System.Collections.Generic;
using Diamond.Basis;

namespace Diamond.Topo
{
	/// <summary>
	/// Capa de limitaciones de velocidad: por cada velocidad, la cobertura del eje
	/// donde rige (como capas de un dibujo). Los tramos de una misma velocidad se
	/// mantienen disjuntos y fusionados vía <see cref="AxisVectorFlex"/>.
	/// Si varias velocidades cubren el mismo PK, la efectiva es la mínima (más restrictiva).
	/// </summary>
	public sealed class SpeedLimitMap
	{
		private readonly Dictionary<int, AxisVectorFlex> mcolBySpeed;

		public SpeedLimitMap()
		{
			mcolBySpeed = new Dictionary<int, AxisVectorFlex>();
		}

		/// <summary>
		/// Coberturas indexadas por velocidad (km/h u otra unidad del dominio).
		/// </summary>
		public IReadOnlyDictionary<int, AxisVectorFlex> BySpeed
		{
			get { return mcolBySpeed; }
		}

		public int SpeedCount
		{
			get { return mcolBySpeed.Count; }
		}

		/// <summary>
		/// Añade un tramo [pk0, pkf) a la capa de <paramref name="speed"/>.
		/// Si pkf &lt; pk0 se normaliza el intervalo.
		/// </summary>
		public void Add(int speed, long pk0, long pkf)
		{
			long start = pk0;
			long end = pkf;
			if (end < start)
			{
				long swap = start;
				start = end;
				end = swap;
			}

			long length = end - start;
			if (length == 0L)
			{
				return;
			}

			AxisVectorFlex flex = GetOrCreateFlex(speed);
			AxisLineal segment = new AxisLineal(start, length);
			flex.Add(segment);
		}

		/// <summary>
		/// Añade un tramo lineal ya definido a la capa de <paramref name="speed"/>.
		/// </summary>
		public void Add(int speed, Lineal<long, LongAxis> segment)
		{
			if (segment is null)
			{
				throw new ArgumentNullException(nameof(segment));
			}

			AxisVectorFlex flex = GetOrCreateFlex(speed);
			flex.Add(segment);
		}

		/// <summary>
		/// Quita cobertura [pk0, pkf) de la capa de <paramref name="speed"/> (si existe).
		/// </summary>
		public void Subtract(int speed, long pk0, long pkf)
		{
			AxisVectorFlex? flex;
			if (!mcolBySpeed.TryGetValue(speed, out flex) || flex is null)
			{
				return;
			}

			long start = pk0;
			long end = pkf;
			if (end < start)
			{
				long swap = start;
				start = end;
				end = swap;
			}

			long length = end - start;
			if (length == 0L)
			{
				return;
			}

			flex.Subtract(new AxisLineal(start, length));
			if (flex.Count == 0)
			{
				mcolBySpeed.Remove(speed);
			}
		}

		/// <summary>
		/// Velocidad más restrictiva (mínima) entre todas las capas que cubren <paramref name="pk"/>.
		/// Null si ninguna limitación aplica en ese punto.
		/// </summary>
		public int? GetMinSpeedAt(long pk)
		{
			int? minSpeed = null;

			foreach (KeyValuePair<int, AxisVectorFlex> pair in mcolBySpeed)
			{
				if (pair.Value.Contains(pk))
				{
					if (!minSpeed.HasValue || pair.Key < minSpeed.Value)
					{
						minSpeed = pair.Key;
					}
				}
			}

			return minSpeed;
		}

		public bool ContainsSpeed(int speed)
		{
			return mcolBySpeed.ContainsKey(speed);
		}

		public AxisVectorFlex? GetCoverage(int speed)
		{
			AxisVectorFlex? flex;
			if (mcolBySpeed.TryGetValue(speed, out flex))
			{
				return flex;
			}

			return null;
		}

		public void Clear()
		{
			mcolBySpeed.Clear();
		}

		/// <summary>
		/// Tramos almacenados por capa (una velocidad por tramo), sin resolver anidamiento.
		/// </summary>
		public IReadOnlyList<SpeedLimitSpan> EnumerateStored()
		{
			List<SpeedLimitSpan> salida = new List<SpeedLimitSpan>();
			foreach (KeyValuePair<int, AxisVectorFlex> pair in mcolBySpeed)
			{
				IReadOnlyList<Lineal<long, LongAxis>> lineals = pair.Value.Lineals;
				int i = 0;
				while (i < lineals.Count)
				{
					Lineal<long, LongAxis> piece = lineals[i];
					salida.Add(new SpeedLimitSpan(piece.PK, piece.PKEnd, pair.Key));
					i++;
				}
			}

			salida.Sort(static (a, b) =>
			{
				int byPk = a.PK.CompareTo(b.PK);
				if (byPk != 0)
				{
					return byPk;
				}

				return a.Speed.CompareTo(b.Speed);
			});
			return salida;
		}

		/// <summary>
		/// Resultado anidado: tramos disjuntos [pk0, pkf) con la velocidad más restrictiva.
		/// 80 en [10,20) + 40 en [15,17) → 80 [10,15), 40 [15,17), 80 [17,20).
		/// </summary>
		public IReadOnlyList<SpeedLimitSpan> Flatten()
		{
			return SpeedLimitFlattener.Flatten(this);
		}

		private AxisVectorFlex GetOrCreateFlex(int speed)
		{
			AxisVectorFlex? flex;
			if (!mcolBySpeed.TryGetValue(speed, out flex) || flex is null)
			{
				flex = new AxisVectorFlex();
				mcolBySpeed[speed] = flex;
			}

			return flex;
		}
	}
}
