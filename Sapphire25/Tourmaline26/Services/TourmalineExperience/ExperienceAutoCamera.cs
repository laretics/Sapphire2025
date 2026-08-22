namespace Tourmaline26.Services.TourmalineExperience
{
	/// <summary>
	/// Sorteo de vista 3D: cenital, drone (lado al azar), frikis y cabina.
	/// No depende de la velocidad.
	/// </summary>
	internal static class ExperienceAutoCamera
	{
		public static readonly TourmalineCameraOrder[] Views =
		[
			TourmalineCameraOrder.Cenital,
			TourmalineCameraOrder.Drone,
			TourmalineCameraOrder.TrackSide,
			TourmalineCameraOrder.Brakeman
		];

		public static (TourmalineCameraOrder Order, bool Side) Pick(
			TourmalineCameraOrder? previous,
			Random? rng = null)
		{
			rng ??= Random.Shared;
			bool side = rng.Next(2) == 1;
			int count = Views.Length;
			int start = rng.Next(count);
			int i = 0;
			while (i < count)
			{
				TourmalineCameraOrder candidate = Views[(start + i) % count];
				if (previous is null || candidate != previous.Value)
					return (candidate, side);
				i++;
			}

			return (Views[start], side);
		}
	}
}
