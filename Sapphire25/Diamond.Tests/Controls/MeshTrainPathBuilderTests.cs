using Diamond.Controls.Rendering;
using Diamond.Motion;
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

			string d = MeshTrainPathBuilder.ToSvgPath(pts, useSpline: true);
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
		public void ToSvgPath_InteractiveLod_UsesPolylineOnly()
		{
			List<MeshTrainPathBuilder.Point> pts = new List<MeshTrainPathBuilder.Point>
			{
				new MeshTrainPathBuilder.Point(0, 100),
				new MeshTrainPathBuilder.Point(20, 90),
				new MeshTrainPathBuilder.Point(40, 60),
				new MeshTrainPathBuilder.Point(60, 40),
				new MeshTrainPathBuilder.Point(80, 20)
			};

			string d = MeshTrainPathBuilder.ToSvgPath(pts, useSpline: false);
			Assert.StartsWith("M", d, StringComparison.Ordinal);
			Assert.Contains(" L", d, StringComparison.Ordinal);
			Assert.DoesNotContain(" C", d, StringComparison.Ordinal);
			Assert.Equal(4, CountChar(d, 'L'));
		}

		[Fact]
		public void InteractiveOptions_DisableSplines()
		{
			Assert.True(MeshSvgDrawOptions.Full.UseSplinePaths);
			Assert.False(MeshSvgDrawOptions.Interactive.UseSplinePaths);
			Assert.False(MeshSvgDrawOptions.Full.ForInteractiveLod().UseSplinePaths);
		}

		private static int CountChar(string s, char c)
		{
			int n = 0;
			int i = 0;
			while (i < s.Length)
			{
				if (s[i] == c)
				{
					n++;
				}

				i++;
			}

			return n;
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
		public void Centripetal_SharpTurn_DoesNotInvertHandleAgainstSegment()
		{
			// Tramo corto tras un salto de Y: la cuerda P0–P2 puede ir en sentido
			// opuesto al tramo P1→P2 (bug típico del atajo (P2−P0) en un sentido de marcha).
			// P0=(0,0), P1=(10,50), P2=(20,45), P3=(30,0) → seg P1→P2 baja levemente.
			MeshTrainPathBuilder.CentripetalSegmentControls(
				0, 0,
				10, 50,
				20, 45,
				30, 0,
				out double c1x, out double c1y,
				out double c2x, out double c2y);

			double segDx = 20.0 - 10.0;
			double segDy = 45.0 - 50.0;
			double len2 = segDx * segDx + segDy * segDy;
			double h1x = c1x - 10.0;
			double h1y = c1y - 50.0;
			double h2x = 20.0 - c2x;
			double h2y = 45.0 - c2y;
			double proj1 = (h1x * segDx + h1y * segDy) / len2;
			double proj2 = (h2x * segDx + h2y * segDy) / len2;

			Assert.True(proj1 >= -1e-6, $"handle1 invertido proj={proj1:F3} c1=({c1x:F2},{c1y:F2})");
			Assert.True(proj2 >= -1e-6, $"handle2 invertido proj={proj2:F3} c2=({c2x:F2},{c2y:F2})");
			Assert.InRange(c1x, 10.0, 20.0);
			Assert.InRange(c2x, 10.0, 20.0);
		}

		[Fact]
		public void Centripetal_RisingAndFalling_HandlesAgreeWithSegmentSlope()
		{
			// Diagonal ascendente en Y (como PK decreciente en pantalla).
			MeshTrainPathBuilder.CentripetalSegmentControls(
				0, 10,
				10, 20,
				30, 40,
				40, 50,
				out double c1x, out double c1y,
				out double c2x, out double c2y);

			// Handles deben tirar en el mismo sentido del tramo (Y crece).
			Assert.True(c1y >= 20.0 - 1e-6, $"c1y={c1y} debería ser >= p1.y");
			Assert.True(c2y <= 40.0 + 1e-6, $"c2y={c2y} debería ser <= p2.y");
			Assert.True(c1x > 10.0 && c1x < 30.0);
			Assert.True(c2x > 10.0 && c2x < 30.0);

			// Diagonal descendente en Y (PK creciente → Y baja).
			MeshTrainPathBuilder.CentripetalSegmentControls(
				0, 50,
				10, 40,
				30, 20,
				40, 10,
				out c1x, out c1y,
				out c2x, out c2y);

			Assert.True(c1y <= 40.0 + 1e-6, $"c1y={c1y} debería ser <= p1.y (bajando)");
			Assert.True(c2y >= 20.0 - 1e-6, $"c2y={c2y} debería ser >= p2.y (bajando)");
			Assert.True(c1x > 10.0 && c1x < 30.0);
			Assert.True(c2x > 10.0 && c2x < 30.0);
		}

		[Fact]
		public void BothWays_ControlHandles_AgreeWithSegmentDirection()
		{
			// Ida y vuelta: en ambos sentidos los handles Bezier no deben invertirse.
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
				req both ways A -> B 06:00-08:00 as R1
				  stops 30s
				  dwell M 3min
				""";
			Assert.True(plan.CompileDemand().Success);
			Mesh mesh = new MeshPlanner(plan).Solve(DayOfWeek.Monday);
			Assert.True(mesh.Circulations.Count >= 2);

			RouteView display = RouteView.FromAxis(axis);
			MeshYScale yScale = MeshYScale.Create(MeshYScaleMode.LinearPk, display, display.PK, display.PKEnd);

			int ci = 0;
			while (ci < mesh.Circulations.Count)
			{
				Circulation c = mesh.Circulations[ci];
				List<MeshTrainPathBuilder.Point> pts = MeshTrainPathBuilder.CollectControlPoints(
					c, display, display.PK, display.PKEnd,
					c.Departure.TotalSeconds - 60, c.Arrival.TotalSeconds + 60,
					40, 36, 800, 600, yScale, 64, false, out _, out _);

				Assert.True(pts.Count >= 4, c.Id + " pocos puntos");

				// Monotonía de X (tiempo).
				int p = 1;
				while (p < pts.Count)
				{
					Assert.True(
						pts[p].X + 1e-6 >= pts[p - 1].X,
						c.Id + " X no monótona en " + p);
					p++;
				}

				// Cada segmento: handles no tiran en sentido Y opuesto al tramo.
				int i = 0;
				int last = pts.Count - 1;
				while (i < last)
				{
					MeshTrainPathBuilder.Point p0 = i == 0 ? pts[0] : pts[i - 1];
					MeshTrainPathBuilder.Point p1 = pts[i];
					MeshTrainPathBuilder.Point p2 = pts[i + 1];
					MeshTrainPathBuilder.Point p3 = i + 1 >= last ? pts[last] : pts[i + 2];

					MeshTrainPathBuilder.CentripetalSegmentControls(
						p0.X, p0.Y, p1.X, p1.Y, p2.X, p2.Y, p3.X, p3.Y,
						out double c1x, out double c1y, out double c2x, out double c2y);

					double segDx = p2.X - p1.X;
					double segDy = p2.Y - p1.Y;
					double h1x = c1x - p1.X;
					double h1y = c1y - p1.Y;
					double h2x = p2.X - c2x;
					double h2y = p2.Y - c2y;

					// Tiempo: handles no deben ir hacia atrás.
					if (segDx > 0.5)
					{
						Assert.True(h1x >= -0.5, $"{c.Id} seg{i}: c1x atrás h1x={h1x}");
						Assert.True(h2x >= -0.5, $"{c.Id} seg{i}: c2x atrás h2x={h2x}");
					}

					// Y: si el tramo tiene pendiente clara, el handle no tira al contrario
					// más allá de una fracción del tramo (inversión visual).
					if (Math.Abs(segDy) > 2.0)
					{
						// Proyección del handle sobre la dirección del tramo debe ser >= 0
						// (o casi nula en dwells).
						double len2 = segDx * segDx + segDy * segDy;
						double proj1 = (h1x * segDx + h1y * segDy) / len2;
						double proj2 = (h2x * segDx + h2y * segDy) / len2;
						Assert.True(
							proj1 >= -0.15,
							$"{c.Id} sense={c.Asimilation.Sense} seg{i}: handle1 invertido proj={proj1:F3} segDy={segDy:F2}");
						Assert.True(
							proj2 >= -0.15,
							$"{c.Id} sense={c.Asimilation.Sense} seg{i}: handle2 invertido proj={proj2:F3} segDy={segDy:F2}");
					}

					i++;
				}

				ci++;
			}
		}

		[Fact]
		public void ReversePathDisplay_MappedPoints_MonotonicInDisplayPkSense()
		{
			// Vista multi-tramo: ida Concat A→B, vuelta es reverse path; display = ida.
			Station stA = MakeStation("A", "STA");
			Station stJ = MakeStation("J", "STJ");
			Station stB = MakeStation("B", "STB");

			Axis ax1 = new Axis();
			ax1.Id = "T3";
			ax1.Vmax = 100;
			AxisVertex a0 = new AxisVertex(39.0, 2.0, 0L);
			a0.Station = stA;
			AxisVertex a1 = new AxisVertex(39.05, 2.05, 15000L);
			a1.Station = stJ;
			ax1.AddVertex(a0);
			ax1.AddVertex(a1);
			ax1.Rebuild();
			ax1.SetCantonFrontiers(new long[] { 0L, 15000L });
			ax1.DefaultTrackCount = 1;

			Axis ax2 = new Axis();
			ax2.Id = "T2";
			ax2.Vmax = 100;
			AxisVertex b0 = new AxisVertex(39.05, 2.05, 0L);
			b0.Station = stJ;
			AxisVertex b1 = new AxisVertex(39.1, 2.1, 12000L);
			b1.Station = stB;
			ax2.AddVertex(b0);
			ax2.AddVertex(b1);
			ax2.Rebuild();
			ax2.SetCantonFrontiers(new long[] { 0L, 12000L });
			ax2.DefaultTrackCount = 1;

			TopoLayout topo = new TopoLayout();
			topo.AddStation(stA);
			topo.AddStation(stJ);
			topo.AddStation(stB);
			topo.AddAxis(ax1);
			topo.AddAxis(ax2);

			Plan plan = new Plan(topo);
			plan.EnsureDefaultTrainSpecs();
			plan.DemandScript = """
				req both ways A -> B 06:00-10:00 as R1
				  stops 30s
				  dwell J 2min
				""";
			Assert.True(plan.CompileDemand().Success, string.Join("; ", plan.CompileDemand().Errors));
			Mesh mesh = new MeshPlanner(plan).Solve(DayOfWeek.Monday);
			Assert.True(mesh.Circulations.Count >= 2);

			// Display = camino A→B (como el selector de vista del UI).
			RouteView? display;
			Assert.True(RouteView.TryFindPath(topo, stA, stB, out display, out _, out _));
			Assert.NotNull(display);
			MeshYScale yScale = MeshYScale.Create(MeshYScaleMode.LinearPk, display, display!.PK, display.PKEnd);

			int foundReverse = 0;
			int ci = 0;
			while (ci < mesh.Circulations.Count)
			{
				Circulation c = mesh.Circulations[ci];
				bool reverse = display.IsReversePath(c.Asimilation.View);
				if (reverse)
				{
					foundReverse++;
				}

				List<MeshTrainPathBuilder.Point> pts = MeshTrainPathBuilder.CollectControlPoints(
					c, display, display.PK, display.PKEnd,
					c.Departure.TotalSeconds - 60, c.Arrival.TotalSeconds + 60,
					40, 36, 800, 600, yScale, 64, false, out _, out _);

				Assert.True(pts.Count >= 4, c.Id + " pts");

				// El sentido de marcha en pantalla: ida Y baja (PK↑), vuelta Y sube (PK↓).
				double yFirst = pts[0].Y;
				double yLast = pts[pts.Count - 1].Y;
				if (reverse)
				{
					// Vuelta B→A en display A→B: PK display decrece → Y crece.
					Assert.True(
						yLast > yFirst + 5.0,
						$"vuelta debería subir en Y: first={yFirst:F1} last={yLast:F1} reverseView={reverse} sense={c.Asimilation.Sense}");
				}
				else
				{
					Assert.True(
						yLast < yFirst - 5.0,
						$"ida debería bajar en Y: first={yFirst:F1} last={yLast:F1}");
				}

				// Handles no invertidos respecto al tramo.
				int last = pts.Count - 1;
				int i = 0;
				while (i < last)
				{
					MeshTrainPathBuilder.Point p0 = i == 0 ? pts[0] : pts[i - 1];
					MeshTrainPathBuilder.Point p1 = pts[i];
					MeshTrainPathBuilder.Point p2 = pts[i + 1];
					MeshTrainPathBuilder.Point p3 = i + 1 >= last ? pts[last] : pts[i + 2];
					MeshTrainPathBuilder.CentripetalSegmentControls(
						p0.X, p0.Y, p1.X, p1.Y, p2.X, p2.Y, p3.X, p3.Y,
						out double c1x, out double c1y, out double c2x, out double c2y);

					double segDx = p2.X - p1.X;
					double segDy = p2.Y - p1.Y;
					if (Math.Abs(segDy) > 2.0 && segDx > 0.5)
					{
						double len2 = segDx * segDx + segDy * segDy;
						double h1x = c1x - p1.X;
						double h1y = c1y - p1.Y;
						double h2x = p2.X - c2x;
						double h2y = p2.Y - c2y;
						double proj1 = (h1x * segDx + h1y * segDy) / len2;
						double proj2 = (h2x * segDx + h2y * segDy) / len2;
						Assert.True(
							proj1 >= -0.2,
							$"{c.Id} rev={reverse} seg{i} handle1 inv proj={proj1:F3}");
						Assert.True(
							proj2 >= -0.2,
							$"{c.Id} rev={reverse} seg{i} handle2 inv proj={proj2:F3}");
					}

					i++;
				}

				ci++;
			}

			Assert.True(foundReverse >= 1, "se esperaba al menos un camino reverse respecto al display");
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

		[Fact]
		public void ComputeLabelPlacements_ShortSegment_TwoPlotLabelsRotated()
		{
			// Trazo corto en el centro del plot: número al inicio y al fin, en el plot.
			double plotLeft = 40;
			double plotTop = 36;
			double plotW = 800;
			double plotH = 600;
			List<MeshTrainPathBuilder.Point> pts = new List<MeshTrainPathBuilder.Point>
			{
				new MeshTrainPathBuilder.Point(200, 300),
				new MeshTrainPathBuilder.Point(280, 260),
				new MeshTrainPathBuilder.Point(360, 220)
			};

			List<MeshTrainPathBuilder.TrainLabelPlacement> places =
				MeshTrainPathBuilder.ComputeLabelPlacements(pts, plotLeft, plotTop, plotW, plotH, "4902");

			Assert.Equal(2, places.Count);
			Assert.All(places, p => Assert.Equal(MeshTrainPathBuilder.TrainLabelBand.Plot, p.Band));
			// El del final queda a continuación del último tramo (pendiente negativa en Y).
			MeshTrainPathBuilder.TrainLabelPlacement atEnd = places[1];
			Assert.True(atEnd.X > 360.0, "final a continuación en X");
			Assert.True(atEnd.Y < 220.0, "final sigue la pendiente hacia arriba");
			Assert.InRange(atEnd.AngleDeg, -80.0, -5.0);
		}

		[Fact]
		public void ComputeLabelPlacements_FullHeight_TopAndBottomRulers()
		{
			// Ascendente: inicio abajo → regla inferior; fin arriba → regla superior.
			// X de cada número = intersección del trazo con esa regla.
			double plotLeft = 40;
			double plotTop = 36;
			double plotW = 800;
			double plotH = 600;
			List<MeshTrainPathBuilder.Point> pts = new List<MeshTrainPathBuilder.Point>
			{
				new MeshTrainPathBuilder.Point(120, plotTop + plotH - 4),
				new MeshTrainPathBuilder.Point(200, plotTop + plotH * 0.5),
				new MeshTrainPathBuilder.Point(280, plotTop + 4)
			};

			List<MeshTrainPathBuilder.TrainLabelPlacement> places =
				MeshTrainPathBuilder.ComputeLabelPlacements(pts, plotLeft, plotTop, plotW, plotH, "4907");

			Assert.Equal(2, places.Count);
			Assert.Equal(MeshTrainPathBuilder.TrainLabelBand.BottomRuler, places[0].Band);
			Assert.Equal(MeshTrainPathBuilder.TrainLabelBand.TopRuler, places[1].Band);
			Assert.InRange(places[0].X, 115.0, 125.0);
			Assert.InRange(places[1].X, 275.0, 285.0);
			Assert.True(places[0].Y > plotTop + plotH);
			Assert.True(places[1].Y < plotTop);
			// Rotados (no 0° forzado): pendiente del trazo.
			Assert.True(Math.Abs(places[0].AngleDeg) > 1.0 || Math.Abs(places[1].AngleDeg) > 1.0);
		}

		[Fact]
		public void ComputeLabelPlacements_Descending_TopStart_BottomEnd()
		{
			// Descendente: inicio arriba → regla superior; fin abajo → regla inferior.
			double plotLeft = 40;
			double plotTop = 36;
			double plotW = 800;
			double plotH = 600;
			List<MeshTrainPathBuilder.Point> pts = new List<MeshTrainPathBuilder.Point>
			{
				new MeshTrainPathBuilder.Point(150, plotTop + 3),
				new MeshTrainPathBuilder.Point(220, plotTop + plotH * 0.5),
				new MeshTrainPathBuilder.Point(300, plotTop + plotH - 3)
			};

			List<MeshTrainPathBuilder.TrainLabelPlacement> places =
				MeshTrainPathBuilder.ComputeLabelPlacements(pts, plotLeft, plotTop, plotW, plotH, "4908");

			Assert.Equal(2, places.Count);
			Assert.Equal(MeshTrainPathBuilder.TrainLabelBand.TopRuler, places[0].Band);
			Assert.Equal(MeshTrainPathBuilder.TrainLabelBand.BottomRuler, places[1].Band);
			Assert.InRange(places[0].X, 145.0, 155.0);
			Assert.InRange(places[1].X, 295.0, 305.0);
		}

		[Fact]
		public void ComputeLabelPlacements_EndOnYAxis_OmitsThatLabel()
		{
			// Inicio en el eje Y (borde izquierdo): no se imprime; el fin sí (en plot).
			double plotLeft = 40;
			double plotTop = 36;
			double plotW = 800;
			double plotH = 600;
			List<MeshTrainPathBuilder.Point> pts = new List<MeshTrainPathBuilder.Point>
			{
				new MeshTrainPathBuilder.Point(plotLeft + 2, 300),
				new MeshTrainPathBuilder.Point(120, 280),
				new MeshTrainPathBuilder.Point(220, 250)
			};

			List<MeshTrainPathBuilder.TrainLabelPlacement> places =
				MeshTrainPathBuilder.ComputeLabelPlacements(pts, plotLeft, plotTop, plotW, plotH, "4910");

			Assert.Single(places);
			Assert.Equal(MeshTrainPathBuilder.TrainLabelBand.Plot, places[0].Band);
			Assert.True(places[0].X > 200.0);
		}

		[Fact]
		public void ReadableAngleDeg_FlipsUpsideDownAngles()
		{
			// Izquierda-arriba: atan2(-1,-1) ≈ -135° → +45° legible.
			double deg = MeshTrainPathBuilder.ReadableAngleDeg(-1.0, -1.0);
			Assert.InRange(deg, -90.0, 90.0);
			Assert.InRange(deg, 40.0, 50.0);

			// Derecha-abajo: sin flip.
			double deg2 = MeshTrainPathBuilder.ReadableAngleDeg(1.0, 1.0);
			Assert.InRange(deg2, 40.0, 50.0);
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
