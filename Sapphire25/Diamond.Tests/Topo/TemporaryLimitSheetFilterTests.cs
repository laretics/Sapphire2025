using Diamond.Topo;

namespace Diamond.Tests.Topo
{
	public class TemporaryLimitSheetFilterTests
	{
		[Fact]
		public void TrackForTrain_Ascending_AlwaysTrack1()
		{
			Assert.Equal(TemporaryLimitTrack.Track1, TemporaryLimitSheetFilter.TrackForTrain(true, 1));
			Assert.Equal(TemporaryLimitTrack.Track1, TemporaryLimitSheetFilter.TrackForTrain(true, 2));
		}

		[Fact]
		public void TrackForTrain_Descending_BabIsTrack2_BauIsTrack1()
		{
			Assert.Equal(TemporaryLimitTrack.Track2, TemporaryLimitSheetFilter.TrackForTrain(false, 2));
			Assert.Equal(TemporaryLimitTrack.Track1, TemporaryLimitSheetFilter.TrackForTrain(false, 1));
		}

		[Fact]
		public void Applies_BothAlways_AndTrack2OnlyOnDescendingBab()
		{
			Assert.True(TemporaryLimitSheetFilter.Applies(TemporaryLimitTrack.Both, true, 2));
			Assert.True(TemporaryLimitSheetFilter.Applies(TemporaryLimitTrack.Both, false, 2));
			Assert.True(TemporaryLimitSheetFilter.Applies(TemporaryLimitTrack.Track1, true, 2));
			Assert.False(TemporaryLimitSheetFilter.Applies(TemporaryLimitTrack.Track2, true, 2));
			Assert.True(TemporaryLimitSheetFilter.Applies(TemporaryLimitTrack.Track2, false, 2));
			Assert.False(TemporaryLimitSheetFilter.Applies(TemporaryLimitTrack.Track2, false, 1));
			Assert.True(TemporaryLimitSheetFilter.Applies(TemporaryLimitTrack.Track1, false, 1));
			Assert.False(TemporaryLimitSheetFilter.Applies(TemporaryLimitTrack.Track1, false, 2));
		}
	}
}
