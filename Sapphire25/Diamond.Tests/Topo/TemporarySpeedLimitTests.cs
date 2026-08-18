using Diamond.Topo;

namespace Diamond.Tests.Topo
{
	public class TemporarySpeedLimitTests
	{
		[Fact]
		public void SpeedLimitSpan_NormalizesInvertedInterval()
		{
			SpeedLimitSpan span = new SpeedLimitSpan(5000L, 1000L, 40);
			Assert.Equal(1000L, span.PK);
			Assert.Equal(5000L, span.PKEnd);
			Assert.Equal(40, span.Speed);
		}

		[Fact]
		public void TemporarySpeedLimit_InheritsLinearSpeedAndDefaults()
		{
			TemporarySpeedLimit limit = new TemporarySpeedLimit(1200L, 44800L, 30);
			limit.AxisId = "T3";
			limit.Track = TemporaryLimitTrack.Track1;
			limit.Reason = TemporaryLimitReason.Works;
			limit.SignaledOnTrack = true;

			Assert.Equal(1200L, limit.PK);
			Assert.Equal(44800L, limit.PKEnd);
			Assert.Equal(30, limit.Speed);
			Assert.Equal("T3", limit.AxisId);
			Assert.Equal(TemporaryLimitTrack.Track1, limit.Track);
			Assert.True(limit.IsNewCreation);
			Assert.Equal(TemporaryLimitReason.Works, limit.Reason);
			Assert.True(limit.SignaledOnTrack);
		}
	}
}
