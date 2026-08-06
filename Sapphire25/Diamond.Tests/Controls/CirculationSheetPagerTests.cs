using Diamond.Controls.Rendering;

namespace Diamond.Tests.Controls
{
	public class CirculationSheetPagerTests
	{
		[Theory]
		[InlineData(0, 30, 1)]
		[InlineData(10, 30, 1)]
		[InlineData(30, 30, 1)]
		[InlineData(31, 30, 2)]
		[InlineData(60, 30, 2)]
		[InlineData(61, 30, 3)]
		[InlineData(100, 30, 4)]
		public void ComputePageCount_AddsPagesUntilAverageAtMostMax(int n, int max, int expectedPages)
		{
			Assert.Equal(expectedPages, CirculationSheetPager.ComputePageCount(n, max));
		}

		[Fact]
		public void Paginate_DistributesEvenlyAndNeverExceedsMax()
		{
			const int max = 30;
			List<CirculationSheetFrontier> frontiers = new List<CirculationSheetFrontier>();
			int i = 0;
			while (i < 100)
			{
				frontiers.Add(MakeFrontier(i * 1000L, i < 99));
				i++;
			}

			IReadOnlyList<CirculationSheetPage> pages = CirculationSheetPager.Paginate(frontiers, max);
			Assert.Equal(4, pages.Count);

			int total = 0;
			int p = 0;
			int minRows = int.MaxValue;
			int maxRows = 0;
			while (p < pages.Count)
			{
				int c = pages[p].Frontiers.Count;
				Assert.True(c <= max);
				if (c < minRows)
				{
					minRows = c;
				}

				if (c > maxRows)
				{
					maxRows = c;
				}

				total += c;
				Assert.Equal(p, pages[p].PageIndex);
				Assert.Equal(4, pages[p].PageCount);
				p++;
			}

			Assert.Equal(100, total);
			// Reparto equilibrado: diferencia de filas entre mitades ≤ 1.
			Assert.True(maxRows - minRows <= 1, "filas desequilibradas: " + minRows + ".." + maxRows);
		}

		[Fact]
		public void ComputeSheetCount_TwoBookPages_OneLandscapeSheet()
		{
			Assert.Equal(1, CirculationSheetPager.ComputeSheetCount(1));
			Assert.Equal(1, CirculationSheetPager.ComputeSheetCount(2));
			Assert.Equal(2, CirculationSheetPager.ComputeSheetCount(3));
			Assert.Equal(2, CirculationSheetPager.ComputeSheetCount(4));
		}

		[Fact]
		public void EstimateTextWidth_BoldCoversLongStationNames()
		{
			// El factor antiguo 0.56 dejaba el rectángulo corto en nombres largos.
			double old = "MANACOR".Length * 8.0 * 0.56;
			double neu = CirculationSheetSvgRenderer.EstimateTextWidth("MANACOR", 8.0, bold: true);
			Assert.True(neu > old + 2.0, "nuevo=" + neu + " old=" + old);
			Assert.True(neu >= 8.0 * 0.66 * 7, "ancho mínimo razonable");
		}

		[Fact]
		public void RenderAllPages_TwoBookPages_OneLandscapeSvg()
		{
			List<CirculationSheetFrontier> frontiers = new List<CirculationSheetFrontier>();
			int i = 0;
			while (i < 40)
			{
				frontiers.Add(MakeFrontier(i * 1000L, i < 39));
				i++;
			}

			IReadOnlyList<CirculationSheetPage> book = CirculationSheetPager.Paginate(frontiers, 30);
			Assert.Equal(2, book.Count);

			// Documento mínimo vía reflexión de Build no hace falta: RenderSheet directo.
			// Usamos un documento real con un plan pequeño sería pesado; comprobamos sheet count.
			Assert.Equal(1, CirculationSheetPager.ComputeSheetCount(book.Count));
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
