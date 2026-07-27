using System;
using System.Collections.Generic;
using Diamond.Topo;

namespace Diamond.Motion
{
	/// <summary>
	/// Asimilación cinemática dinámica: perfil de velocidad a lo largo de una <see cref="RouteView"/>
	/// a partir de <see cref="TrainSpecs"/>, limitaciones de los ejes físicos, origen/destino (sentido)
	/// y paradas intermedias. El tren parte del origen a v=0 y termina en destino a v=0.
	/// Los PK de origen/destino/paradas/muestras son PK de ruta de la vista.
	/// </summary>
	public sealed class Asimilation
	{
		private const double MetersPerSecondPerKmh = 1.0 / 3.6;
		private const long SampleStepMeters = 5L;
		private const double MinSpeedMetersPerSecond = 1e-6;

		private readonly RouteView mvarView;
		private readonly TrainSpecs mvarSpecs;
		private readonly StationOnAxis mvarOrigin;
		private readonly StationOnAxis mvarDestination;
		private readonly CirculationSense mvarSense;
		private readonly List<AsimilationStop> mcolStops;

		/// <summary>
		/// Muestras en orden de marcha (índice 0 = origen, último = destino). El PK de ruta puede subir o bajar.
		/// </summary>
		private long[] mcolSamplePk = Array.Empty<long>();
		private double[] mcolSpeedMs = Array.Empty<double>();
		private double[] mcolTimeSeconds = Array.Empty<double>();
		private bool mvarIsBuilt;

		/// <summary>
		/// Atajo de un solo eje: envuelve el eje en <see cref="RouteView.FromAxis"/> y usa sus PK como PK de ruta.
		/// </summary>
		public Asimilation(
			Axis axis,
			TrainSpecs specs,
			StationOnAxis origin,
			StationOnAxis destination,
			IReadOnlyList<AsimilationStop>? intermediateStops = null)
			: this(
				RouteView.FromAxis(axis ?? throw new ArgumentNullException(nameof(axis))),
				specs,
				origin,
				destination,
				intermediateStops)
		{
		}

		public Asimilation(
			RouteView view,
			TrainSpecs specs,
			StationOnAxis origin,
			StationOnAxis destination,
			IReadOnlyList<AsimilationStop>? intermediateStops = null)
		{
			if (view is null)
			{
				throw new ArgumentNullException(nameof(view));
			}

			if (specs is null)
			{
				throw new ArgumentNullException(nameof(specs));
			}

			if (origin is null)
			{
				throw new ArgumentNullException(nameof(origin));
			}

			if (destination is null)
			{
				throw new ArgumentNullException(nameof(destination));
			}

			if (origin.PK == destination.PK)
			{
				throw new ArgumentException("Origen y destino deben tener PK distintos.", nameof(destination));
			}

			mvarView = view;
			mvarSpecs = specs;
			mvarOrigin = origin;
			mvarDestination = destination;
			mvarSense = origin.PK < destination.PK
				? CirculationSense.IncreasingPk
				: CirculationSense.DecreasingPk;

			mcolStops = new List<AsimilationStop>();
			if (intermediateStops is not null)
			{
				int index = 0;
				while (index < intermediateStops.Count)
				{
					AsimilationStop? stop = intermediateStops[index];
					if (stop is null)
					{
						throw new ArgumentException("La lista de paradas no puede contener null.", nameof(intermediateStops));
					}

					if (!IsPkOnPath(stop.PK, origin.PK, destination.PK))
					{
						throw new ArgumentException(
							$"La parada en PK={stop.PK} no está entre origen ({origin.PK}) y destino ({destination.PK}).",
							nameof(intermediateStops));
					}

					// No duplicar origen/destino como "intermedia"; el dwell allí se puede modelar aparte más adelante.
					if (stop.PK != origin.PK && stop.PK != destination.PK)
					{
						mcolStops.Add(stop);
					}

					index++;
				}
			}

			SortStopsAlongPath();
			mvarIsBuilt = false;
			Rebuild();
		}

		/// <summary>
		/// Vista de ruta sobre la que está calculada la marcha.
		/// </summary>
		public RouteView View
		{
			get { return mvarView; }
		}

		/// <summary>
		/// Primer eje físico de la vista (compatibilidad con código mono-eje).
		/// Preferir <see cref="View"/> para multi-eje.
		/// </summary>
		public Axis Axis
		{
			get { return mvarView.Legs[0].Axis; }
		}

		public TrainSpecs Specs
		{
			get { return mvarSpecs; }
		}

		/// <summary>
		/// Origen en PK de ruta de <see cref="View"/>.
		/// </summary>
		public StationOnAxis Origin
		{
			get { return mvarOrigin; }
		}

		/// <summary>
		/// Destino en PK de ruta de <see cref="View"/>.
		/// </summary>
		public StationOnAxis Destination
		{
			get { return mvarDestination; }
		}

		/// <summary>
		/// Sentido de circulación inferido de origen → destino.
		/// </summary>
		public CirculationSense Sense
		{
			get { return mvarSense; }
		}

		/// <summary>
		/// Paradas intermedias (sin origen ni destino), en orden de marcha.
		/// </summary>
		public IReadOnlyList<AsimilationStop> Stops
		{
			get { return mcolStops; }
		}

		/// <summary>
		/// Duración total del recorrido (incluye detenciones intermedias) desde la salida del origen.
		/// </summary>
		public TimeSpan TotalTime
		{
			get
			{
				EnsureBuilt();
				if (mcolTimeSeconds.Length == 0)
				{
					return TimeSpan.Zero;
				}

				return TimeSpan.FromSeconds(mcolTimeSeconds[mcolTimeSeconds.Length - 1]);
			}
		}

		/// <summary>
		/// Velocidad de circulación en el PK de ruta <paramref name="pk"/> (km/h).
		/// Fuera del tramo origen–destino devuelve 0.
		/// </summary>
		public double SpeedByPK(long pk)
		{
			EnsureBuilt();
			if (mcolSamplePk.Length == 0)
			{
				return 0.0;
			}

			if (!IsPkOnPath(pk, mvarOrigin.PK, mvarDestination.PK))
			{
				return 0.0;
			}

			int index = FindPathIndexAtOrBeforePk(pk);
			if (index < 0)
			{
				return 0.0;
			}

			if (index >= mcolSamplePk.Length - 1)
			{
				return MsToKmh(mcolSpeedMs[mcolSpeedMs.Length - 1]);
			}

			long pk0 = mcolSamplePk[index];
			long pk1 = mcolSamplePk[index + 1];
			if (pk1 == pk0)
			{
				return MsToKmh(mcolSpeedMs[index]);
			}

			double t = (double)(pk - pk0) / (double)(pk1 - pk0);
			// t en [0,1] si pk está entre pk0 y pk1 en valor; con PK decreciente pk1<pk0 sigue siendo válido.
			double v = mcolSpeedMs[index] + t * (mcolSpeedMs[index + 1] - mcolSpeedMs[index]);
			return MsToKmh(v);
		}

		/// <summary>
		/// Instantes relativos a la salida en el PK de ruta <paramref name="pk"/>
		/// (interpolación sobre el perfil). Fuera de ruta devuelve null.
		/// </summary>
		public TimeSpan? TimeByPK(long pk)
		{
			EnsureBuilt();
			if (mcolSamplePk.Length == 0)
			{
				return null;
			}

			if (!IsPkOnPath(pk, mvarOrigin.PK, mvarDestination.PK))
			{
				return null;
			}

			int index = FindPathIndexAtOrBeforePk(pk);
			if (index < 0)
			{
				return null;
			}

			if (index >= mcolSamplePk.Length - 1)
			{
				return TimeSpan.FromSeconds(mcolTimeSeconds[mcolTimeSeconds.Length - 1]);
			}

			long pk0 = mcolSamplePk[index];
			long pk1 = mcolSamplePk[index + 1];
			double t0 = mcolTimeSeconds[index];
			double t1 = mcolTimeSeconds[index + 1];
			if (pk1 == pk0)
			{
				return TimeSpan.FromSeconds(t0);
			}

			double u = (double)(pk - pk0) / (double)(pk1 - pk0);
			double seconds = t0 + u * (t1 - t0);
			return TimeSpan.FromSeconds(seconds);
		}

		/// <summary>
		/// PK de ruta en el instante <paramref name="time"/> desde la salida del origen (v=0).
		/// Tiempos posteriores al final del recorrido devuelven el PK del destino.
		/// </summary>
		public long PKByTime(TimeSpan time)
		{
			EnsureBuilt();
			if (mcolSamplePk.Length == 0)
			{
				return mvarOrigin.PK;
			}

			double seconds = time.TotalSeconds;
			if (seconds <= 0.0)
			{
				return mcolSamplePk[0];
			}

			if (seconds >= mcolTimeSeconds[mcolTimeSeconds.Length - 1])
			{
				return mcolSamplePk[mcolSamplePk.Length - 1];
			}

			int index = FindTimeIndexAtOrBefore(seconds);
			if (index < 0)
			{
				return mcolSamplePk[0];
			}

			if (index >= mcolSamplePk.Length - 1)
			{
				return mcolSamplePk[mcolSamplePk.Length - 1];
			}

			double t0 = mcolTimeSeconds[index];
			double t1 = mcolTimeSeconds[index + 1];
			long pk0 = mcolSamplePk[index];
			long pk1 = mcolSamplePk[index + 1];

			if (t1 <= t0)
			{
				return pk0;
			}

			double u = (seconds - t0) / (t1 - t0);
			double pk = pk0 + u * (pk1 - pk0);
			return (long)Math.Round(pk);
		}

		/// <summary>
		/// Recalcula el perfil (p. ej. tras cambiar limitaciones de los ejes de la vista).
		/// </summary>
		public void Rebuild()
		{
			mvarIsBuilt = false;

			long pkStart = mvarOrigin.PK;
			long pkEnd = mvarDestination.PK;

			List<long> samples = BuildSamplePksAlongPath(pkStart, pkEnd);
			int n = samples.Count;
			if (n == 0)
			{
				mcolSamplePk = Array.Empty<long>();
				mcolSpeedMs = Array.Empty<double>();
				mcolTimeSeconds = Array.Empty<double>();
				mvarIsBuilt = true;
				return;
			}

			double[] vLimit = new double[n];
			int i = 0;
			while (i < n)
			{
				long pk = samples[i];
				if (IsMandatoryStopPk(pk))
				{
					vLimit[i] = 0.0;
				}
				else
				{
					vLimit[i] = ResolveLimitMs(pk);
				}

				i++;
			}

			double a = mvarSpecs.Acceleration;
			double b = mvarSpecs.ServiceBrake;

			// Envolvente de frenado (hacia atrás en el orden de marcha).
			double[] vBrake = new double[n];
			vBrake[n - 1] = vLimit[n - 1];
			i = n - 2;
			while (i >= 0)
			{
				double ds = PathDistanceMeters(samples[i], samples[i + 1]);
				double next = vBrake[i + 1];
				double reachable = Math.Sqrt(next * next + 2.0 * b * ds);
				vBrake[i] = Math.Min(vLimit[i], reachable);
				i--;
			}

			// Envolvente de aceleración (hacia adelante desde el origen a v=0).
			double[] vAccel = new double[n];
			vAccel[0] = 0.0;
			i = 1;
			while (i < n)
			{
				double ds = PathDistanceMeters(samples[i - 1], samples[i]);
				double prev = vAccel[i - 1];
				double reachable = Math.Sqrt(prev * prev + 2.0 * a * ds);
				vAccel[i] = Math.Min(vLimit[i], reachable);
				i++;
			}

			double[] vProfile = new double[n];
			i = 0;
			while (i < n)
			{
				vProfile[i] = Math.Min(vLimit[i], Math.Min(vBrake[i], vAccel[i]));
				if (vProfile[i] < 0.0)
				{
					vProfile[i] = 0.0;
				}

				i++;
			}

			double[] timeSec = new double[n];
			timeSec[0] = 0.0;
			Dictionary<long, double> dwellByPk = BuildDwellLookup();

			i = 1;
			while (i < n)
			{
				double ds = PathDistanceMeters(samples[i - 1], samples[i]);
				double v0 = vProfile[i - 1];
				double v1 = vProfile[i];
				double dtMove = EstimateSegmentTimeSeconds(ds, v0, v1, a, b);
				timeSec[i] = timeSec[i - 1] + dtMove;

				double dwell;
				if (dwellByPk.TryGetValue(samples[i], out dwell) && dwell > 0.0)
				{
					timeSec[i] += dwell;
				}

				i++;
			}

			mcolSamplePk = samples.ToArray();
			mcolSpeedMs = vProfile;
			mcolTimeSeconds = timeSec;
			mvarIsBuilt = true;
		}

		private void EnsureBuilt()
		{
			if (!mvarIsBuilt)
			{
				Rebuild();
			}
		}

		private void SortStopsAlongPath()
		{
			if (mvarSense == CirculationSense.IncreasingPk)
			{
				mcolStops.Sort(static (left, right) => left.PK.CompareTo(right.PK));
			}
			else
			{
				mcolStops.Sort(static (left, right) => right.PK.CompareTo(left.PK));
			}
		}

		/// <summary>
		/// Muestras en orden de marcha desde origen hasta destino.
		/// </summary>
		private List<long> BuildSamplePksAlongPath(long pkStart, long pkEnd)
		{
			SortedSet<long> set = new SortedSet<long>();
			set.Add(pkStart);
			set.Add(pkEnd);

			long minPk = pkStart < pkEnd ? pkStart : pkEnd;
			long maxPk = pkStart > pkEnd ? pkStart : pkEnd;
			long pk = minPk;
			while (pk < maxPk)
			{
				pk += SampleStepMeters;
				if (pk > maxPk)
				{
					pk = maxPk;
				}

				set.Add(pk);
			}

			int index = 0;
			while (index < mcolStops.Count)
			{
				set.Add(mcolStops[index].PK);
				index++;
			}

			List<long> ordered = new List<long>(set.Count);
			if (mvarSense == CirculationSense.IncreasingPk)
			{
				foreach (long value in set)
				{
					ordered.Add(value);
				}
			}
			else
			{
				// PK decreciente: recorrido de mayor a menor.
				List<long> ascending = new List<long>(set);
				int i = ascending.Count - 1;
				while (i >= 0)
				{
					ordered.Add(ascending[i]);
					i--;
				}
			}

			return ordered;
		}

		private bool IsMandatoryStopPk(long pk)
		{
			if (pk == mvarOrigin.PK || pk == mvarDestination.PK)
			{
				return true;
			}

			int index = 0;
			while (index < mcolStops.Count)
			{
				if (mcolStops[index].PK == pk)
				{
					return true;
				}

				index++;
			}

			return false;
		}

		private Dictionary<long, double> BuildDwellLookup()
		{
			Dictionary<long, double> dwellByPk = new Dictionary<long, double>();
			int index = 0;
			while (index < mcolStops.Count)
			{
				AsimilationStop stop = mcolStops[index];
				long pk = stop.PK;
				double seconds = stop.Dwell.TotalSeconds;

				double existing;
				if (dwellByPk.TryGetValue(pk, out existing))
				{
					dwellByPk[pk] = existing + seconds;
				}
				else
				{
					dwellByPk[pk] = seconds;
				}

				index++;
			}

			return dwellByPk;
		}

		private double ResolveLimitMs(long routePk)
		{
			int? limitKmh = mvarView.GetEffectiveSpeedLimit(routePk);
			double kmh;
			if (limitKmh.HasValue)
			{
				kmh = limitKmh.Value;
			}
			else
			{
				kmh = mvarSpecs.MaxSpeedKmh;
			}

			if (kmh > mvarSpecs.MaxSpeedKmh)
			{
				kmh = mvarSpecs.MaxSpeedKmh;
			}

			if (kmh < 0.0)
			{
				kmh = 0.0;
			}

			return KmhToMs(kmh);
		}

		private static double PathDistanceMeters(long pkA, long pkB)
		{
			long d = pkB - pkA;
			if (d < 0L)
			{
				d = -d;
			}

			return (double)d;
		}

		private static bool IsPkOnPath(long pk, long originPk, long destinationPk)
		{
			long min = originPk < destinationPk ? originPk : destinationPk;
			long max = originPk > destinationPk ? originPk : destinationPk;
			return pk >= min && pk <= max;
		}

		private static double EstimateSegmentTimeSeconds(double ds, double v0, double v1, double a, double b)
		{
			if (ds <= 0.0)
			{
				return 0.0;
			}

			double vAvg = 0.5 * (v0 + v1);
			if (vAvg < MinSpeedMetersPerSecond)
			{
				if (v1 > MinSpeedMetersPerSecond && a > 0.0)
				{
					return v1 / a;
				}

				if (v0 > MinSpeedMetersPerSecond && b > 0.0)
				{
					return v0 / b;
				}

				return 0.0;
			}

			return ds / vAvg;
		}

		/// <summary>
		/// Índice de muestra en orden de marcha tal que el PK cae en el tramo [i, i+1].
		/// </summary>
		private int FindPathIndexAtOrBeforePk(long pk)
		{
			if (mcolSamplePk.Length == 0)
			{
				return -1;
			}

			if (mvarSense == CirculationSense.IncreasingPk)
			{
				int lo = 0;
				int hi = mcolSamplePk.Length - 1;
				int best = 0;
				while (lo <= hi)
				{
					int mid = lo + (hi - lo) / 2;
					if (mcolSamplePk[mid] == pk)
					{
						return mid;
					}

					if (mcolSamplePk[mid] < pk)
					{
						best = mid;
						lo = mid + 1;
					}
					else
					{
						hi = mid - 1;
					}
				}

				return best;
			}
			else
			{
				// Muestras en PK decreciente.
				int lo = 0;
				int hi = mcolSamplePk.Length - 1;
				int best = 0;
				while (lo <= hi)
				{
					int mid = lo + (hi - lo) / 2;
					if (mcolSamplePk[mid] == pk)
					{
						return mid;
					}

					if (mcolSamplePk[mid] > pk)
					{
						best = mid;
						lo = mid + 1;
					}
					else
					{
						hi = mid - 1;
					}
				}

				return best;
			}
		}

		private int FindTimeIndexAtOrBefore(double seconds)
		{
			int lo = 0;
			int hi = mcolTimeSeconds.Length - 1;
			int best = 0;
			while (lo <= hi)
			{
				int mid = lo + (hi - lo) / 2;
				if (mcolTimeSeconds[mid] == seconds)
				{
					return mid;
				}

				if (mcolTimeSeconds[mid] < seconds)
				{
					best = mid;
					lo = mid + 1;
				}
				else
				{
					hi = mid - 1;
				}
			}

			return best;
		}

		private static double KmhToMs(double kmh)
		{
			return kmh * MetersPerSecondPerKmh;
		}

		private static double MsToKmh(double ms)
		{
			return ms / MetersPerSecondPerKmh;
		}
	}
}
