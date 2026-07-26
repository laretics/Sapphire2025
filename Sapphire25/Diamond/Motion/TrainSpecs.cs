using System;

namespace Diamond.Motion
{
	/// <summary>
	/// Características cinemáticas de un tipo de tren (valores de servicio con margen,
	/// no los máximos de esfuerzo del material). Elemento de catálogo seleccionable en un plan.
	/// </summary>
	public sealed class TrainSpecs
	{
		private string mvarId;
		private string mvarName;
		private double mvarAcceleration;
		private double mvarServiceBrake;
		private double mvarMaxSpeedKmh;

		public TrainSpecs(
			string id,
			string name,
			double accelerationMetersPerSecondSquared,
			double serviceBrakeMetersPerSecondSquared,
			double maxSpeedKmh)
		{
			if (accelerationMetersPerSecondSquared <= 0.0)
			{
				throw new ArgumentOutOfRangeException(nameof(accelerationMetersPerSecondSquared));
			}

			if (serviceBrakeMetersPerSecondSquared <= 0.0)
			{
				throw new ArgumentOutOfRangeException(nameof(serviceBrakeMetersPerSecondSquared));
			}

			if (maxSpeedKmh <= 0.0)
			{
				throw new ArgumentOutOfRangeException(nameof(maxSpeedKmh));
			}

			mvarId = id ?? string.Empty;
			mvarName = name ?? string.Empty;
			mvarAcceleration = accelerationMetersPerSecondSquared;
			mvarServiceBrake = serviceBrakeMetersPerSecondSquared;
			mvarMaxSpeedKmh = maxSpeedKmh;
		}

		/// <summary>
		/// Tren modelo por defecto: 0.9 m/s² acel., 0.8 m/s² freno de servicio, techo 160 km/h.
		/// </summary>
		public static TrainSpecs DefaultModel
		{
			get
			{
				return new TrainSpecs("default", "Modelo", 0.9, 0.8, 160.0);
			}
		}

		/// <summary>
		/// Identificador estable en el catálogo del plan.
		/// </summary>
		public string Id
		{
			get { return mvarId; }
			set { mvarId = value ?? string.Empty; }
		}

		public string Name
		{
			get { return mvarName; }
			set { mvarName = value ?? string.Empty; }
		}

		/// <summary>
		/// Aceleración de servicio (m/s²), con margen respecto al máximo real.
		/// </summary>
		public double Acceleration
		{
			get { return mvarAcceleration; }
			set
			{
				if (value <= 0.0)
				{
					throw new ArgumentOutOfRangeException(nameof(value));
				}

				mvarAcceleration = value;
			}
		}

		/// <summary>
		/// Deceleración de freno de servicio (m/s², magnitud positiva).
		/// </summary>
		public double ServiceBrake
		{
			get { return mvarServiceBrake; }
			set
			{
				if (value <= 0.0)
				{
					throw new ArgumentOutOfRangeException(nameof(value));
				}

				mvarServiceBrake = value;
			}
		}

		/// <summary>
		/// Techo de velocidad del material (km/h) cuando la vía no impone otro menor.
		/// </summary>
		public double MaxSpeedKmh
		{
			get { return mvarMaxSpeedKmh; }
			set
			{
				if (value <= 0.0)
				{
					throw new ArgumentOutOfRangeException(nameof(value));
				}

				mvarMaxSpeedKmh = value;
			}
		}

		public override string ToString()
		{
			if (mvarName.Length > 0)
			{
				return mvarName;
			}

			return mvarId;
		}
	}
}
