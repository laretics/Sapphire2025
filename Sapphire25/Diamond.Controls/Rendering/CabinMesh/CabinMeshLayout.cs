using System;

namespace Diamond.Controls.Rendering.CabinMesh
{
	/// <summary>
	/// Transformación fija de la malla de cabina:
	/// 2 km por detrás + 4 km por delante; tren al inicio del tercio inferior;
	/// X centrado en la hora actual.
	/// </summary>
	public sealed class CabinMeshLayout
	{
		public const long BehindMeters = 2000L;
		public const long AheadMeters = 4000L;
		public const long TotalSpanMeters = BehindMeters + AheadMeters;

		/// <summary>
		/// Minutos a la izquierda (pasado): media hora anterior.
		/// Ventana total = 1 h (media hora atrás + media hora adelante).
		/// </summary>
		public const double PastMinutes = 30.0;

		/// <summary>Minutos a la derecha (futuro): media hora siguiente.</summary>
		public const double FutureMinutes = 30.0;

		private readonly double mvarWidth;
		private readonly double mvarHeight;
		private readonly long mvarPkCenter;
		private readonly bool mvarIncreasingPk;
		private readonly double mvarNowSeconds;
		private readonly double mvarTrainY;
		private readonly long mvarPkBehind;
		private readonly long mvarPkAhead;

		public CabinMeshLayout(
			double width,
			double height,
			long routePkCenter,
			bool increasingPk,
			TimeSpan clockTimeOfDay)
		{
			mvarWidth = width > 10 ? width : 10;
			mvarHeight = height > 10 ? height : 10;
			mvarPkCenter = routePkCenter;
			mvarIncreasingPk = increasingPk;
			// Hora actual en el centro del eje X.
			mvarNowSeconds = clockTimeOfDay.TotalSeconds;

			// Inicio del tercio inferior ≈ 2/3 desde arriba.
			mvarTrainY = mvarHeight * (2.0 / 3.0);

			if (increasingPk)
			{
				mvarPkBehind = mvarPkCenter - BehindMeters;
				mvarPkAhead = mvarPkCenter + AheadMeters;
			}
			else
			{
				mvarPkBehind = mvarPkCenter + BehindMeters;
				mvarPkAhead = mvarPkCenter - AheadMeters;
			}
		}

		/// <summary>
		/// Ventana temporal de 1 h: media hora anterior … media hora siguiente respecto a “ahora”.
		/// </summary>
		public static void GetOneHourWindow(TimeSpan clockTimeOfDay, out double minSeconds, out double maxSeconds)
		{
			double now = clockTimeOfDay.TotalSeconds;
			minSeconds = now - PastMinutes * 60.0;
			maxSeconds = now + FutureMinutes * 60.0;
		}

		public double Width
		{
			get { return mvarWidth; }
		}

		public double Height
		{
			get { return mvarHeight; }
		}

		public long PkCenter
		{
			get { return mvarPkCenter; }
		}

		public long PkBehind
		{
			get { return mvarPkBehind; }
		}

		public long PkAhead
		{
			get { return mvarPkAhead; }
		}

		public bool IncreasingPk
		{
			get { return mvarIncreasingPk; }
		}

		public double TrainY
		{
			get { return mvarTrainY; }
		}

		public double NowSeconds
		{
			get { return mvarNowSeconds; }
		}

		public double TimeMinSeconds
		{
			get { return mvarNowSeconds - PastMinutes * 60.0; }
		}

		public double TimeMaxSeconds
		{
			get { return mvarNowSeconds + FutureMinutes * 60.0; }
		}

		/// <summary>
		/// Y de pantalla para un PK de ruta (abajo = recorrido, arriba = por recorrer).
		/// </summary>
		public double YFromRoutePk(long routePk)
		{
			// Distancia firmada “hacia delante” en el sentido de marcha.
			double forward;
			if (mvarIncreasingPk)
			{
				forward = routePk - mvarPkCenter;
			}
			else
			{
				forward = mvarPkCenter - routePk;
			}

			// forward en [-Behind, +Ahead] → y
			// forward = -Behind → y = height (abajo)
			// forward = 0 → y = TrainY
			// forward = +Ahead → y = 0 (arriba)
			if (forward <= 0)
			{
				double t = (forward + BehindMeters) / BehindMeters; // 0..1
				t = Math.Clamp(t, 0.0, 1.0);
				return mvarHeight - t * (mvarHeight - mvarTrainY);
			}
			else
			{
				double t = forward / AheadMeters; // 0..1
				t = Math.Clamp(t, 0.0, 1.0);
				return mvarTrainY * (1.0 - t);
			}
		}

		public double XFromTimeSeconds(double timeSeconds)
		{
			double min = TimeMinSeconds;
			double max = TimeMaxSeconds;
			double span = max - min;
			if (span < 1.0)
			{
				span = 1.0;
			}

			double t = (timeSeconds - min) / span;
			return t * mvarWidth;
		}

		public double XFromTime(TimeSpan timeOfDay)
		{
			return XFromTimeSeconds(timeOfDay.TotalSeconds);
		}

		public bool IsRoutePkVisible(long routePk)
		{
			long lo = Math.Min(mvarPkBehind, mvarPkAhead);
			long hi = Math.Max(mvarPkBehind, mvarPkAhead);
			return routePk >= lo && routePk <= hi;
		}

		public bool IsTimeVisible(double timeSeconds)
		{
			return timeSeconds >= TimeMinSeconds && timeSeconds <= TimeMaxSeconds;
		}

		/// <summary>
		/// Tiempo de día (segundos) desde X de pantalla.
		/// </summary>
		public double TimeSecondsFromX(double x)
		{
			double t = x / mvarWidth;
			return TimeMinSeconds + t * (TimeMaxSeconds - TimeMinSeconds);
		}

		public long RoutePkFromY(double y)
		{
			if (y >= mvarTrainY)
			{
				// Zona recorrida (debajo del tren).
				double denom = mvarHeight - mvarTrainY;
				if (denom < 1e-6)
				{
					return mvarPkCenter;
				}

				double t = (mvarHeight - y) / denom; // 0 abajo → 1 en tren
				t = Math.Clamp(t, 0.0, 1.0);
				double forward = -BehindMeters + t * BehindMeters;
				return ApplyForward(forward);
			}
			else
			{
				double denom = mvarTrainY;
				if (denom < 1e-6)
				{
					return mvarPkCenter;
				}

				double t = 1.0 - (y / denom); // 0 en tren → 1 arriba
				t = Math.Clamp(t, 0.0, 1.0);
				double forward = t * AheadMeters;
				return ApplyForward(forward);
			}
		}

		private long ApplyForward(double forwardMeters)
		{
			if (mvarIncreasingPk)
			{
				return mvarPkCenter + (long)Math.Round(forwardMeters);
			}

			return mvarPkCenter - (long)Math.Round(forwardMeters);
		}
	}
}
