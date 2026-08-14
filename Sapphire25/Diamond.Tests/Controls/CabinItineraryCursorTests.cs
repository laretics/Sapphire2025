using Diamond.Controls.Rendering;

namespace Diamond.Tests.Controls
{
	public class CabinItineraryCursorTests
	{
		[Fact]
		public void Empty_IsNone()
		{
			CabinItineraryCursor c = CabinItineraryCursor.Resolve(
				Array.Empty<CirculationSheetFrontier>(),
				1000);
			Assert.Equal(CabinItineraryCursorKind.None, c.Kind);
		}

		[Fact]
		public void NearStation_SelectsThatFrontier()
		{
			List<CirculationSheetFrontier> rows = TwoStops(0, 5000);
			CabinItineraryCursor c = CabinItineraryCursor.Resolve(rows, 80, stationAreaMeters: 250);
			Assert.Equal(CabinItineraryCursorKind.Station, c.Kind);
			Assert.Equal(0, c.FrontierIndex);
		}

		[Fact]
		public void MidSegment_SelectsTheLeg()
		{
			List<CirculationSheetFrontier> rows = TwoStops(0, 5000);
			CabinItineraryCursor c = CabinItineraryCursor.Resolve(rows, 2500, stationAreaMeters: 250);
			Assert.Equal(CabinItineraryCursorKind.Segment, c.Kind);
			Assert.Equal(0, c.FrontierIndex);
			Assert.Equal(1, c.SegmentEndIndex);
			Assert.InRange(c.Progress, 0.45, 0.55);
		}

		[Fact]
		public void NearDestination_SelectsLastStation()
		{
			List<CirculationSheetFrontier> rows = TwoStops(0, 5000);
			CabinItineraryCursor c = CabinItineraryCursor.Resolve(rows, 4900, stationAreaMeters: 250);
			Assert.Equal(CabinItineraryCursorKind.Station, c.Kind);
			Assert.Equal(1, c.FrontierIndex);
		}

		[Fact]
		public void DecreasingPk_StillFindsSegment()
		{
			List<CirculationSheetFrontier> rows = TwoStops(8000, 1000);
			CabinItineraryCursor c = CabinItineraryCursor.Resolve(rows, 4000, stationAreaMeters: 250);
			Assert.Equal(CabinItineraryCursorKind.Segment, c.Kind);
			Assert.Equal(0, c.FrontierIndex);
			Assert.InRange(c.Progress, 0.55, 0.60);
		}

		[Fact]
		public void BeyondEnds_ClampsToNearestStation()
		{
			List<CirculationSheetFrontier> rows = TwoStops(1000, 4000);
			CabinItineraryCursor before = CabinItineraryCursor.Resolve(rows, 0, stationAreaMeters: 250);
			Assert.Equal(CabinItineraryCursorKind.Station, before.Kind);
			Assert.Equal(0, before.FrontierIndex);

			CabinItineraryCursor after = CabinItineraryCursor.Resolve(rows, 9000, stationAreaMeters: 250);
			Assert.Equal(CabinItineraryCursorKind.Station, after.Kind);
			Assert.Equal(1, after.FrontierIndex);
		}

		[Fact]
		public void SpeedLimitDependency_CountsAsPointIfClose()
		{
			List<CirculationSheetFrontier> rows = new List<CirculationSheetFrontier>
			{
				Make(0, "ORIGEN", CirculationSheetMarkKind.PrincipalStation, origin: true),
				Make(2000, "PK 2.0", CirculationSheetMarkKind.SpeedLimitChange),
				Make(5000, "DESTINO", CirculationSheetMarkKind.PrincipalStation, dest: true)
			};
			CabinItineraryCursor c = CabinItineraryCursor.Resolve(rows, 2050, stationAreaMeters: 250);
			Assert.Equal(CabinItineraryCursorKind.Station, c.Kind);
			Assert.Equal(1, c.FrontierIndex);
		}

		private static List<CirculationSheetFrontier> TwoStops(long a, long b)
		{
			return new List<CirculationSheetFrontier>
			{
				Make(a, "A", CirculationSheetMarkKind.PrincipalStation, origin: true),
				Make(b, "B", CirculationSheetMarkKind.PrincipalStation, dest: true)
			};
		}

		private static CirculationSheetFrontier Make(
			long pk,
			string name,
			CirculationSheetMarkKind kind,
			bool origin = false,
			bool dest = false)
		{
			return new CirculationSheetFrontier(
				pk,
				(pk / 1000.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture),
				name,
				kind,
				origin,
				dest,
				isCommercialStop: origin || dest,
				dwell: TimeSpan.Zero,
				arrival: TimeSpan.FromHours(7),
				departure: TimeSpan.FromHours(7),
				outgoingTrackCount: 2,
				outgoingVmaxKmh: 100,
				grantedToNext: TimeSpan.FromMinutes(3));
		}
	}
}
