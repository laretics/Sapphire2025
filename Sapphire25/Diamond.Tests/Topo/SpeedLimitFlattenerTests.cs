using Diamond.Topo;

namespace Diamond.Tests.Topo
{
	public class SpeedLimitFlattenerTests
	{
		[Fact]
		public void Flatten_NestedTemporary_SplitsIntoThree()
		{
			SpeedLimitMap map = new SpeedLimitMap();
			map.Add(80, 10L, 20L);
			map.Add(40, 15L, 17L);

			IReadOnlyList<SpeedLimitSpan> flat = map.Flatten();
			Assert.Equal(3, flat.Count);
			Assert.Equal(10L, flat[0].PK);
			Assert.Equal(15L, flat[0].PKEnd);
			Assert.Equal(80, flat[0].Speed);
			Assert.Equal(15L, flat[1].PK);
			Assert.Equal(17L, flat[1].PKEnd);
			Assert.Equal(40, flat[1].Speed);
			Assert.Equal(17L, flat[2].PK);
			Assert.Equal(20L, flat[2].PKEnd);
			Assert.Equal(80, flat[2].Speed);
		}

		[Fact]
		public void FlattenCombined_WithFixed_AddsMoreCuts()
		{
			SpeedLimitMap fixedMap = new SpeedLimitMap();
			fixedMap.Add(60, 12L, 18L);

			List<TemporarySpeedLimit> temps = new List<TemporarySpeedLimit>
			{
				new TemporarySpeedLimit(10L, 20L, 80),
				new TemporarySpeedLimit(15L, 17L, 40)
			};

			IReadOnlyList<SpeedLimitSpan> flat = SpeedLimitFlattener.FlattenCombined(
				fixedMap,
				temps,
				track: null);

			// Cortes: 10, 12, 15, 17, 18, 20
			// 10-12: 80
			// 12-15: min(80,60)=60
			// 15-17: min(80,40,60)=40
			// 17-18: min(80,60)=60
			// 18-20: 80
			Assert.Equal(5, flat.Count);
			Assert.Equal(80, flat[0].Speed);
			Assert.Equal(10L, flat[0].PK);
			Assert.Equal(12L, flat[0].PKEnd);
			Assert.Equal(60, flat[1].Speed);
			Assert.Equal(40, flat[2].Speed);
			Assert.Equal(15L, flat[2].PK);
			Assert.Equal(17L, flat[2].PKEnd);
			Assert.Equal(60, flat[3].Speed);
			Assert.Equal(80, flat[4].Speed);
			Assert.Equal(18L, flat[4].PK);
			Assert.Equal(20L, flat[4].PKEnd);
		}

		[Fact]
		public void FlattenTemporary_TrackFilter_IgnoresOtherTrack()
		{
			TemporarySpeedLimit both = new TemporarySpeedLimit(10L, 20L, 80);
			both.Track = TemporaryLimitTrack.Both;
			TemporarySpeedLimit only1 = new TemporarySpeedLimit(15L, 17L, 40);
			only1.Track = TemporaryLimitTrack.Track1;
			TemporarySpeedLimit only2 = new TemporarySpeedLimit(12L, 14L, 30);
			only2.Track = TemporaryLimitTrack.Track2;

			List<TemporarySpeedLimit> temps = new List<TemporarySpeedLimit> { both, only1, only2 };

			IReadOnlyList<SpeedLimitSpan> track1 = SpeedLimitFlattener.FlattenTemporary(
				temps,
				TemporaryLimitTrack.Track1);
			Assert.Equal(3, track1.Count);
			Assert.Equal(80, track1[0].Speed);
			Assert.Equal(40, track1[1].Speed);
			Assert.Equal(80, track1[2].Speed);

			IReadOnlyList<SpeedLimitSpan> track2 = SpeedLimitFlattener.FlattenTemporary(
				temps,
				TemporaryLimitTrack.Track2);
			Assert.Equal(3, track2.Count);
			Assert.Equal(30, track2[1].Speed);
			Assert.Equal(12L, track2[1].PK);
			Assert.Equal(14L, track2[1].PKEnd);
		}
	}
}
