using Diamond.Controls.Rendering;

namespace Diamond.Tests.Controls
{
	public class CirculationSheetPagerTests
	{
		[Theory]
		[InlineData(0, 36, 1)]
		[InlineData(10, 36, 1)]
		[InlineData(36, 36, 1)]
		[InlineData(37, 36, 2)]
		[InlineData(72, 36, 2)]
		[InlineData(73, 36, 3)]
		[InlineData(100, 36, 3)]
		public void ComputePageCount_AddsPagesUntilAverageAtMostMax(int n, int max, int expectedPages)
		{
			Assert.Equal(expectedPages, CirculationSheetPager.ComputePageCount(n, max));
		}

		[Fact]
		public void Paginate_DistributesEvenlyAndNeverExceedsMax()
		{
			const int max = 36;
			List<CirculationSheetFrontier> frontiers = new List<CirculationSheetFrontier>();
			int i = 0;
			while (i < 100)
			{
				frontiers.Add(MakeFrontier(i * 1000L, i < 99));
				i++;
			}

			IReadOnlyList<CirculationSheetPage> pages = CirculationSheetPager.Paginate(frontiers, max);
			Assert.Equal(3, pages.Count);

			int total = 0;
			int p = 0;
			while (p < pages.Count)
			{
				Assert.True(pages[p].Frontiers.Count <= max);
				total += pages[p].Frontiers.Count;
				Assert.Equal(p, pages[p].PageIndex);
				Assert.Equal(3, pages[p].PageCount);
				p++;
			}

			Assert.Equal(100, total);
			Assert.True((double)total / pages.Count <= max + 1e-9);
		}

		[Fact]
		public void FormatSheetTime_HalfMinutes()
		{
			Assert.Equal("18.02", CirculationSheetDocument.FormatSheetTime(new TimeSpan(18, 2, 0)));
			Assert.Equal("18.02½", CirculationSheetDocument.FormatSheetTime(new TimeSpan(18, 2, 30)));
			Assert.Equal("18.03", CirculationSheetDocument.FormatSheetTime(new TimeSpan(18, 2, 50)));
		}

		[Fact]
		public void FormatCommercialDwell_CircleUnderOneMinute()
		{
			string t = CirculationSheetDocument.FormatCommercialDwell(TimeSpan.FromSeconds(30), out bool circle);
			Assert.True(circle);
			Assert.Equal(string.Empty, t);

			t = CirculationSheetDocument.FormatCommercialDwell(TimeSpan.FromMinutes(2), out circle);
			Assert.False(circle);
			Assert.Equal("2", t);

			t = CirculationSheetDocument.FormatCommercialDwell(TimeSpan.Zero, out circle);
			Assert.False(circle);
			Assert.Equal(string.Empty, t);
		}

		[Fact]
		public void FormatGrantedMinutes_UsesHalves()
		{
			Assert.Equal("3", CirculationSheetDocument.FormatGrantedMinutes(TimeSpan.FromMinutes(3)));
			Assert.Equal("1½", CirculationSheetDocument.FormatGrantedMinutes(TimeSpan.FromMinutes(1.5)));
			Assert.Equal("½", CirculationSheetDocument.FormatGrantedMinutes(TimeSpan.FromSeconds(30)));
		}

		private static CirculationSheetFrontier MakeFrontier(long pk, bool hasOutgoing)
		{
			return new CirculationSheetFrontier(
				pk,
				CirculationSheetDocument.FormatStationKm(pk),
				"ST",
				CirculationSheetMarkKind.Halt,
				false,
				false,
				false,
				TimeSpan.Zero,
				null,
				null,
				hasOutgoing ? 1 : null,
				hasOutgoing ? 80 : null,
				hasOutgoing ? TimeSpan.FromMinutes(2) : null);
		}
	}
}
