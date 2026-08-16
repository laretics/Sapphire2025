using Diamond.Cabin;

namespace Diamond.Tests.Cabin
{
	public class CabinEnvironmentSearchTests
	{
		[Theory]
		[InlineData("8", 8, 0)]
		[InlineData("08", 8, 0)]
		[InlineData("8:30", 8, 30)]
		[InlineData("08:30", 8, 30)]
		[InlineData("8.30", 8, 30)]
		[InlineData("830", 8, 30)]
		[InlineData("0830", 8, 30)]
		[InlineData("0", 0, 0)]
		[InlineData("23:59", 23, 59)]
		public void TryParseClock_AcceptsCivilTimes(string token, int hours, int minutes)
		{
			Assert.True(CabinEnvironment.TryParseClock(token, out TimeSpan time));
			Assert.Equal(hours, time.Hours);
			Assert.Equal(minutes, time.Minutes);
			Assert.Equal(0, time.Days);
		}

		[Theory]
		[InlineData("")]
		[InlineData("24")]
		[InlineData("24:00")]
		[InlineData("8:60")]
		[InlineData("4701")]
		[InlineData("abc")]
		public void TryParseClock_RejectsNonHours(string token)
		{
			Assert.False(CabinEnvironment.TryParseClock(token, out _));
		}
	}
}
