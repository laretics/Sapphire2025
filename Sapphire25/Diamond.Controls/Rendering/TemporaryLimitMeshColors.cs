namespace Diamond.Controls.Rendering
{
	/// <summary>
	/// Paleta de la malla para limitaciones temporales (distintas de las fijas).
	/// Amarillo si V ≥ 50; naranja fosforito si V &lt; 50.
	/// </summary>
	public static class TemporaryLimitMeshColors
	{
		public const int SpeedThresholdKmh = 50;

		/// <summary>Amarillo de temporales con V ≥ 50.</summary>
		public const string Yellow = "#ffd400";

		/// <summary>Naranja fosforito de temporales con V &lt; 50.</summary>
		public const string FluorescentOrange = "#ff5f1f";

		public static string ForSpeed(int speedKmh)
		{
			if (speedKmh < SpeedThresholdKmh)
			{
				return FluorescentOrange;
			}

			return Yellow;
		}
	}
}
