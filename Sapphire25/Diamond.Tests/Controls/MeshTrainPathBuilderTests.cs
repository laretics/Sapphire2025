using Diamond.Controls.Rendering;
using Diamond.Timed;
using Diamond.Topo;

namespace Diamond.Tests.Controls
{
	public class MeshTrainPathBuilderTests
	{
		[Fact]
		public void ToSvgPath_TwoPoints_IsLine()
		{
			List<MeshTrainPathBuilder.Point> pts = new List<MeshTrainPathBuilder.Point>
			{
				new MeshTrainPathBuilder.Point(10, 20),
				new MeshTrainPathBuilder.Point(40, 80)
			};

			string d = MeshTrainPathBuilder.ToSvgPath(pts);
			Assert.StartsWith("M", d, StringComparison.Ordinal);
			Assert.Contains(" L", d, StringComparison.Ordinal);
			Assert.DoesNotContain(" C", d, StringComparison.Ordinal);
		}

		[Fact]
		public void ToSvgPath_ManyPoints_UsesCubicBeziers()
		{
			List<MeshTrainPathBuilder.Point> pts = new List<MeshTrainPathBuilder.Point>
			{
				new MeshTrainPathBuilder.Point(0, 100),
				new MeshTrainPathBuilder.Point(20, 90),
				new MeshTrainPathBuilder.Point(40, 60),
				new MeshTrainPathBuilder.Point(60, 40),
				new MeshTrainPathBuilder.Point(80, 20)
			};

			string d = MeshTrainPathBuilder.ToSvgPath(pts);
			Assert.StartsWith("M", d, StringComparison.Ordinal);
			Assert.Contains(" C", d, StringComparison.Ordinal);
			// 4 segmentos cúbicos entre 5 puntos.
			int cubics = 0;
			int i = 0;
			while (i < d.Length)
			{
				if (d[i] == 'C')
				{
					cubics++;
				}

				i++;
			}

			Assert.Equal(4, cubics);
		}

		[Fact]
		public void Centripetal_HorizontalSegment_KeepsControlsNearLine()
		{
			// P0..P3 colineales horizontales: los controles no deben desviarse en Y.
			MeshTrainPathBuilder.CentripetalSegmentControls(
				0, 50,
				10, 50,
				30, 50,
				40, 50,
				out double c1x, out double c1y,
				out double c2x, out double c2y);

			Assert.InRange(c1y, 49.5, 50.5);
			Assert.InRange(c2y, 49.5, 50.5);
			Assert.True(c1x > 10.0 && c1x < 30.0);
			Assert.True(c2x > 10.0 && c2x < 30.0);
		}

		[Fact]
		public void BuildSvgPath_RealTrip_ProducesSmoothPathWithStops()
		{
			Circulation c = PlanTripWithCommercialStop();
			RouteView view = c.Asimilation.View;
			MeshYScale yScale = MeshYScale.Create(MeshYScaleMode.LinearPk, view, view.PK, view.PKEnd);

			string d = MeshTrainPathBuilder.BuildSvgPath(
				c,
				view,
				view.PK,
				view.PKEnd,
				t0: c.Departure.TotalSeconds - 60,
				t1: c.Arrival.TotalSeconds + 60,
				plotLeft: 40,
				plotTop: 36,
				plotW: 800,
				plotH: 600,
				yScale: yScale,
				maxSamples: 96,
				wantLabel: true,
				out double labelX,
				out double labelY);

			Assert.False(string.IsNullOrEmpty(d));
			Assert.Contains(" C", d, StringComparison.Ordinal);
			Assert.False(double.IsNaN(labelX));
			Assert.False(double.IsNaN(labelY));

			// Debe haber muestreado la parada comercial (llegada y salida en M).
			List<MeshTrainPathBuilder.Point> pts = MeshTrainPathBuilder.CollectControlPoints(
				c, view, view.PK, view.PKEnd,
				c.Departure.TotalSeconds - 60, c.Arrival.TotalSeconds + 60,
				40, 36, 800, 600, yScale, 96, false, out _, out _);
			Assert.True(pts.Count >= 16, "se esperan bastantes puntos de control");
		}

		private static Circulation PlanTripWithCommercialStop()
		{
			Station stA = MakeStation("A", "STA");
			Station stM = MakeStation("M", "STM");
			Station stB = MakeStation("B", "STB");

			Axis axis = new Axis();
			axis.Id = "X1";
			axis.Vmax = 100;
			AxisVertex v0 = new AxisVertex(39.0, 2.0, 0L);
			v0.Station = stA;
			AxisVertex v1 = new AxisVertex(39.05, 2.05, 10000L);
			v1.Station = stM;
			AxisVertex v2 = new AxisVertex(39.1, 2.1, 20000L);
			v2.Station = stB;
			axis.AddVertex(v0);
			axis.AddVertex(v1);
			axis.AddVertex(v2);
			axis.Rebuild();
			axis.SetCantonFrontiers(new long[] { 0L, 10000L, 20000L });
			axis.DefaultTrackCount = 2;

			TopoLayout topo = new TopoLayout();
			topo.AddStation(stA);
			topo.AddStation(stM);
			topo.AddStation(stB);
			topo.AddAxis(axis);

			Plan plan = new Plan(topo);
			plan.EnsureDefaultTrainSpecs();
			plan.DemandScript = """
				req A -> B 06:00-08:00 as R1
				  stops 30s
				  dwell M 5min
				""";
			Assert.True(plan.CompileDemand().Success);
			Mesh mesh = new MeshPlanner(plan).Solve(DayOfWeek.Monday);
			Assert.NotEmpty(mesh.Circulations);
			return mesh.Circulations[0];
		}

		private static Station MakeStation(string id, string avr)
		{
			Station s = new Station(id);
			s.Name = id;
			s.Avr = avr;
			return s;
		}
	}
}
