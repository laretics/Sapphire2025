using Tourmaline26.Logic;

namespace Tourmaline26.Tests;

public sealed class UnscheduledCirculationTests
{
	[Theory]
	[InlineData("1234")]
	[InlineData("T11")]
	[InlineData("8105")]
	[InlineData("r-21")]
	[InlineData("AB12")]
	[InlineData("8")]
	[InlineData("0830")]
	public void LooksLikeTrainToken_accepts_service_numbers(string token)
	{
		Assert.True(UnscheduledCirculation.LooksLikeTrainToken(token));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("Palma")]
	[InlineData("8:30")]
	[InlineData("08.30")]
	[InlineData("destino")]
	[InlineData("1234567890123")]
	public void LooksLikeTrainToken_rejects_hours_and_names(string? token)
	{
		Assert.False(UnscheduledCirculation.LooksLikeTrainToken(token));
	}

	[Fact]
	public void NormalizeToken_trims_and_uppercases()
	{
		Assert.Equal("T11", UnscheduledCirculation.NormalizeToken(" t11 "));
	}

	[Fact]
	public void Session_has_active_train_without_diamond_or_zafiro()
	{
		var session = new SessionConfiguration();
		Assert.False(session.HasActiveTrain);

		session.UnscheduledTrainToken = "1234";
		Assert.True(session.HasUnscheduledTrain);
		Assert.True(session.HasActiveTrain);

		session.ClearUnscheduledTrain();
		Assert.False(session.HasActiveTrain);
	}
}
