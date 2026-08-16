using Sapphire2025Models.Diamond;

namespace Diamond.Tests.Timed
{
	public class FestiveStoreDateTests
	{
		[Fact]
		public void TryParseIso_AcceptsCivilDates()
		{
			DateTime date;
			Assert.True(FestiveDate.TryParseIso("2026-08-15", out date));
			Assert.Equal(2026, date.Year);
			Assert.Equal(8, date.Month);
			Assert.Equal(15, date.Day);
			Assert.Equal(0, date.Hour);
			Assert.Equal(DateTimeKind.Unspecified, date.Kind);
		}

		[Fact]
		public void TryParseIso_RejectsGarbage()
		{
			DateTime date;
			Assert.False(FestiveDate.TryParseIso("15/08/2026", out date));
			Assert.False(FestiveDate.TryParseIso("", out date));
			Assert.False(FestiveDate.TryParseIso(null, out date));
		}

		[Fact]
		public void ToIso_IsStableRoundTrip()
		{
			DateTime source = new DateTime(2026, 12, 25, 18, 40, 0, DateTimeKind.Local);
			string iso = FestiveDate.ToIso(source);
			Assert.Equal("2026-12-25", iso);
			DateTime back;
			Assert.True(FestiveDate.TryParseIso(iso, out back));
			Assert.Equal(FestiveDate.Normalize(source), back);
		}
	}
}
