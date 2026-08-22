using Tourmaline26.Services.OpenMeteo;

namespace Tourmaline26.Services.TourmalineExperience
{
	/// <summary>
	/// Open-Meteo (WMO + nubes/lluvia/visibilidad) → clima de Tourmaline Experience.
	/// </summary>
	internal static class ExperienceWeather
	{
		public static TourmalineExperienceService.SampleWeatherType From(WeatherValue? weather)
		{
			if (weather is null)
				return TourmalineExperienceService.SampleWeatherType.Sunny;

			int code = weather.WeatherCode;
			double rain = weather.Rain;
			double clouds = weather.CloudCover;
			double visibility = weather.Visibility;

			if (code is 45 or 48)
				return TourmalineExperienceService.SampleWeatherType.Foggy;
			if (visibility > 0 && visibility < 500 && rain < 0.2 && code is >= 0 and <= 3)
				return TourmalineExperienceService.SampleWeatherType.Foggy;

			if (code is 71 or 73 or 75 or 77 or 85 or 86)
				return TourmalineExperienceService.SampleWeatherType.Snow;

			if (code is 65 or 82 or 95 or 96 or 99)
				return TourmalineExperienceService.SampleWeatherType.HeavyRain;
			if (code is 55 or 63 or 67 or 81)
				return TourmalineExperienceService.SampleWeatherType.ModerateRain;
			if (code is 51 or 53 or 56 or 57 or 61 or 66 or 80)
				return TourmalineExperienceService.SampleWeatherType.LightRain;

			if (rain >= 4)
				return TourmalineExperienceService.SampleWeatherType.HeavyRain;
			if (rain >= 1.5)
				return TourmalineExperienceService.SampleWeatherType.ModerateRain;
			if (rain >= 0.2)
				return TourmalineExperienceService.SampleWeatherType.LightRain;

			if (code == 3 || clouds >= 85)
				return TourmalineExperienceService.SampleWeatherType.Overcast;
			if (code == 2 || clouds >= 55)
				return TourmalineExperienceService.SampleWeatherType.ModerateCloudy;
			if (code == 1 || clouds >= 25)
				return TourmalineExperienceService.SampleWeatherType.LightCloudy;

			return TourmalineExperienceService.SampleWeatherType.Sunny;
		}
	}
}
