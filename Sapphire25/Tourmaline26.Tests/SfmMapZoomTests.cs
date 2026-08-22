using Tourmaline26.Logic;

namespace Tourmaline26.Tests;

public sealed class SfmMapZoomTests
{
	[Fact]
	public void ForSpeed_at_rest_with_factor_one_is_at_rest_zoom()
	{
		Assert.Equal(SfmMapZoom.AtRest, SfmMapZoom.ForSpeed(0, 1), 5);
	}

	[Fact]
	public void ForSpeed_at_max_speed_drops_by_span()
	{
		double expected = SfmMapZoom.AtRest - SfmMapZoom.SpeedSpan;
		Assert.Equal(expected, SfmMapZoom.ForSpeed(SfmMapZoom.MaxSpeedKmh, 1), 5);
	}

	[Fact]
	public void ForSpeed_multiplies_base_by_factor()
	{
		double atRest = SfmMapZoom.ForSpeed(0, 1);
		Assert.Equal(atRest * 1.2, SfmMapZoom.ForSpeed(0, 1.2), 5);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(double.NaN)]
	public void ClampFactor_invalid_becomes_one(double factor)
	{
		Assert.Equal(1, SfmMapZoom.ClampFactor(factor));
	}

	[Fact]
	public void ForSpeed_clamps_to_max_zoom()
	{
		Assert.Equal(SfmMapZoom.MaxZoom, SfmMapZoom.ForSpeed(0, 2));
	}

	[Fact]
	public void ForSpeed_production_factor_is_closer_than_default()
	{
		double def = SfmMapZoom.ForSpeed(80, 1);
		double prod = SfmMapZoom.ForSpeed(80, 1.2);
		Assert.True(prod > def);
	}
}
