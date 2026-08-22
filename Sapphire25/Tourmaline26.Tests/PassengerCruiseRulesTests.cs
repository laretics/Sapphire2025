using Tourmaline26.Logic;

namespace Tourmaline26.Tests;

public sealed class PassengerCruiseRulesTests
{
	[Theory]
	[InlineData(0, 50_000, true)]
	[InlineData(19, 50_000, true)]
	[InlineData(20, 50_000, false)]
	[InlineData(80, 50_000, false)]
	[InlineData(80, 999, true)]
	[InlineData(80, 1000, false)]
	[InlineData(19, 1000, true)]
	[InlineData(20, 0, true)]
	public void ShowNextStopsList_speed_or_distance(int speed, long remaining, bool expected)
	{
		Assert.Equal(expected, PassengerCruiseRules.ShowNextStopsList(speed, remaining));
	}
}
