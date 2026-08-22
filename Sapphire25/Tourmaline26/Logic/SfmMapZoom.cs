namespace Tourmaline26.Logic
{
	/// <summary>
	/// Zoom MapLibre del overlay SFM: baja al acelerar, sube al parar.
	/// El factor (appsettings) multiplica el zoom base; en 1024×768 suele
	/// hacer falta &gt; 1 para que las vías no se vean como un hilo.
	/// </summary>
	internal static class SfmMapZoom
	{
		public const double AtRest = 11.4;
		public const double SpeedSpan = 2.3;
		public const double MaxSpeedKmh = 110;
		public const double MinZoom = 7.5;
		public const double MaxZoom = 14;

		public static double ClampFactor(double factor)
		{
			if (double.IsNaN(factor) || double.IsInfinity(factor) || factor <= 0)
				return 1;
			return Math.Clamp(factor, 0.5, 2);
		}

		public static double ForSpeed(double kmh, double factor)
		{
			double speed = Math.Clamp(kmh, 0, MaxSpeedKmh);
			double baseZoom = AtRest - (speed / MaxSpeedKmh) * SpeedSpan;
			return Math.Clamp(baseZoom * ClampFactor(factor), MinZoom, MaxZoom);
		}
	}
}
