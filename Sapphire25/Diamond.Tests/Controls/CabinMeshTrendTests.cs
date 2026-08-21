using Diamond.Controls.Rendering.CabinMesh;

namespace Diamond.Tests.Controls
{
	public class CabinMeshTrendTests
	{
		private static CabinMeshLayout Layout()
		{
			return new CabinMeshLayout(600, 600, 10000, true, TimeSpan.FromHours(12));
		}

		[Fact]
		public void TrendLine_IsAbsentAtOrBelowFiveKmh()
		{
			CabinMeshLayout layout = Layout();
			double x0, y0, x1, y1;
			Assert.False(layout.TryGetTrendLine(0, out x0, out y0, out x1, out y1));
			Assert.False(layout.TryGetTrendLine(5, out x0, out y0, out x1, out y1));
			Assert.False(layout.TryGetTrendLine(CabinMeshLayout.TrendMinSpeedKmh, out x0, out y0, out x1, out y1));
		}

		[Fact]
		public void TrendLine_IsPresentAboveFiveKmh()
		{
			CabinMeshLayout layout = Layout();
			double x0, y0, x1, y1;
			Assert.True(layout.TryGetTrendLine(5.1, out x0, out y0, out x1, out y1));
			Assert.Equal(layout.Width / 2.0, x0, 3);
			Assert.Equal(layout.TrainY, y0, 3);
			Assert.True(x1 > x0);
			Assert.True(y1 < y0);
		}

		[Fact]
		public void TrendLine_HighSpeedHitsTopBeforeRightEdge()
		{
			CabinMeshLayout layout = Layout();
			// 72 km/h = 20 m/s → 4 km adelante en 200 s ≪ 30 min.
			Assert.True(layout.TryGetTrendLine(72, out double x0, out double y0, out double x1, out double y1));
			Assert.Equal(0.0, y1, 3);
			Assert.True(x1 < layout.Width - 1.0);
			double dt = 200.0;
			double expectedX = layout.XFromTimeSeconds(layout.NowSeconds + dt);
			Assert.Equal(expectedX, x1, 2);
			Assert.Equal(layout.TrainY, y0, 3);
			Assert.Equal(layout.Width / 2.0, x0, 3);
		}

		[Fact]
		public void TrendLine_LowSpeedHitsRightEdgeBeforeTop()
		{
			CabinMeshLayout layout = Layout();
			// 6 km/h = 1.667 m/s → 3 km en 30 min, no llega a los 4 km del techo.
			Assert.True(layout.TryGetTrendLine(6, out _, out double y0, out double x1, out double y1));
			Assert.Equal(layout.Width, x1, 3);
			Assert.True(y1 > 0.0);
			Assert.True(y1 < y0);
			double forward = 6.0 * (1000.0 / 3600.0) * (CabinMeshLayout.FutureMinutes * 60.0);
			double expectedY = layout.TrainY * (1.0 - forward / CabinMeshLayout.AheadMeters);
			Assert.Equal(expectedY, y1, 2);
		}

		[Fact]
		public void TrendLine_SteeperWhenFaster()
		{
			CabinMeshLayout layout = Layout();
			Assert.True(layout.TryGetTrendLine(20, out double ax0, out double ay0, out double ax1, out double ay1));
			Assert.True(layout.TryGetTrendLine(80, out double bx0, out double by0, out double bx1, out double by1));
			double slopeSlow = (ay1 - ay0) / (ax1 - ax0);
			double slopeFast = (by1 - by0) / (bx1 - bx0);
			// Y baja al avanzar: pendiente más negativa = más rápido.
			Assert.True(slopeFast < slopeSlow);
		}

		[Fact]
		public void TrendLine_PassesThroughEtaOfStationAhead()
		{
			CabinMeshLayout layout = Layout();
			const double speedKmh = 36.0; // 10 m/s
			const double stationAheadM = 2000.0;
			double vMs = speedKmh * (1000.0 / 3600.0);
			double etaSec = stationAheadM / vMs; // 200 s
			double etaX = layout.XFromTimeSeconds(layout.NowSeconds + etaSec);
			double stationY = layout.YFromRoutePk(layout.PkCenter + (long)stationAheadM);

			Assert.True(layout.TryGetTrendLine(speedKmh, out double x0, out double y0, out double x1, out double y1));
			Assert.True(etaX > x0 && etaX < x1 + 0.5);
			double u = (etaX - x0) / (x1 - x0);
			double yOnTrend = y0 + u * (y1 - y0);
			Assert.Equal(stationY, yOnTrend, 1);
		}

		[Fact]
		public void TrendLine_DecreasingPkStillGoesForwardUp()
		{
			CabinMeshLayout layout = new CabinMeshLayout(600, 600, 10000, false, TimeSpan.FromHours(12));
			Assert.True(layout.TryGetTrendLine(36, out double x0, out double y0, out double x1, out double y1));
			Assert.True(x1 > x0);
			Assert.True(y1 < y0);
		}

		[Fact]
		public void Svg_AlwaysDrawsHorizontalPkLineWithFadeMask()
		{
			CabinMeshLayout layout = Layout();
			CabinMeshSvgBuilder.Result result = CabinMeshSvgBuilder.Build(
				layout,
				CabinMeshPalette.Day,
				view: null,
				dayCirculations: null,
				active: null,
				nightMode: false);

			Assert.Contains("cabin-mesh-train-pk", result.SvgMarkup, StringComparison.Ordinal);
			Assert.Contains("url(#cabinMeshHMask)", result.SvgMarkup, StringComparison.Ordinal);
			Assert.DoesNotContain("cabin-mesh-trend", result.SvgMarkup, StringComparison.Ordinal);
		}

		[Fact]
		public void Svg_DrawsDashDotTrendOnlyAboveThreshold()
		{
			CabinMeshLayout layout = Layout();
			CabinMeshSvgBuilder.Result stopped = CabinMeshSvgBuilder.Build(
				layout,
				CabinMeshPalette.Day,
				view: null,
				dayCirculations: null,
				active: null,
				nightMode: false,
				currentSpeedKmh: 4);

			Assert.DoesNotContain("cabin-mesh-trend", stopped.SvgMarkup, StringComparison.Ordinal);

			CabinMeshSvgBuilder.Result moving = CabinMeshSvgBuilder.Build(
				layout,
				CabinMeshPalette.Night,
				view: null,
				dayCirculations: null,
				active: null,
				nightMode: true,
				currentSpeedKmh: 40);

			Assert.Contains("cabin-mesh-trend", moving.SvgMarkup, StringComparison.Ordinal);
			Assert.Contains("stroke-dasharray=\"2 5 10 5\"", moving.SvgMarkup, StringComparison.Ordinal);
			Assert.Contains(CabinMeshPalette.Night.NowLine, moving.SvgMarkup, StringComparison.Ordinal);
		}
	}
}
