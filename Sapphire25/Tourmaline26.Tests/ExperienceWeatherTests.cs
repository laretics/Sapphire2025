using Tourmaline26.Services.OpenMeteo;
using Tourmaline26.Services.TourmalineExperience;

namespace Tourmaline26.Tests;

public sealed class ExperienceWeatherTests
{
	[Theory]
	[InlineData(0, TourmalineExperienceService.SampleWeatherType.Sunny)]
	[InlineData(1, TourmalineExperienceService.SampleWeatherType.LightCloudy)]
	[InlineData(2, TourmalineExperienceService.SampleWeatherType.ModerateCloudy)]
	[InlineData(3, TourmalineExperienceService.SampleWeatherType.Overcast)]
	[InlineData(45, TourmalineExperienceService.SampleWeatherType.Foggy)]
	[InlineData(48, TourmalineExperienceService.SampleWeatherType.Foggy)]
	[InlineData(51, TourmalineExperienceService.SampleWeatherType.LightRain)]
	[InlineData(61, TourmalineExperienceService.SampleWeatherType.LightRain)]
	[InlineData(63, TourmalineExperienceService.SampleWeatherType.ModerateRain)]
	[InlineData(65, TourmalineExperienceService.SampleWeatherType.HeavyRain)]
	[InlineData(71, TourmalineExperienceService.SampleWeatherType.Snow)]
	[InlineData(75, TourmalineExperienceService.SampleWeatherType.Snow)]
	[InlineData(80, TourmalineExperienceService.SampleWeatherType.LightRain)]
	[InlineData(82, TourmalineExperienceService.SampleWeatherType.HeavyRain)]
	[InlineData(95, TourmalineExperienceService.SampleWeatherType.HeavyRain)]
	public void From_wmo_code(int code, TourmalineExperienceService.SampleWeatherType expected)
	{
		var weather = new WeatherValue { WeatherCode = code, Visibility = 10000 };
		Assert.Equal(expected, ExperienceWeather.From(weather));
	}

	[Fact]
	public void From_null_is_sunny()
	{
		Assert.Equal(
			TourmalineExperienceService.SampleWeatherType.Sunny,
			ExperienceWeather.From(null));
	}

	[Fact]
	public void From_low_visibility_without_rain_is_fog()
	{
		var weather = new WeatherValue
		{
			WeatherCode = 2,
			Visibility = 200,
			Rain = 0
		};
		Assert.Equal(
			TourmalineExperienceService.SampleWeatherType.Foggy,
			ExperienceWeather.From(weather));
	}

	[Fact]
	public void From_rain_mm_overrides_clear_sky()
	{
		var weather = new WeatherValue
		{
			WeatherCode = 0,
			Rain = 2.0,
			Visibility = 8000
		};
		Assert.Equal(
			TourmalineExperienceService.SampleWeatherType.ModerateRain,
			ExperienceWeather.From(weather));
	}
}
