using Diamond.Controls.Rendering;
using Diamond.Motion;
using Diamond.Timed;
using Diamond.Topo;

namespace Diamond.Tests.Controls
{
	public class TemporaryLimitMeshColorTests
	{
		[Fact]
		public void ForSpeed_YellowAtOrAbove50_OrangeBelow()
		{
			Assert.Equal(TemporaryLimitMeshColors.Yellow, TemporaryLimitMeshColors.ForSpeed(50));
			Assert.Equal(TemporaryLimitMeshColors.Yellow, TemporaryLimitMeshColors.ForSpeed(80));
			Assert.Equal(TemporaryLimitMeshColors.FluorescentOrange, TemporaryLimitMeshColors.ForSpeed(49));
			Assert.Equal(TemporaryLimitMeshColors.FluorescentOrange, TemporaryLimitMeshColors.ForSpeed(30));
		}

		[Fact]
		public void RenderContent_TemporaryLimits_UseYellowAndFluorescentOrange()
		{
			(Mesh mesh, RouteView view) = BuildMeshWithTemps();

			string svg = MeshSvgRenderer.RenderContent(
				mesh, view,
				TimeSpan.FromHours(6), TimeSpan.FromHours(10),
				0, 20000,
				width: 800, height: 500,
				MeshSvgDrawOptions.Full);

			Assert.Contains(TemporaryLimitMeshColors.Yellow, svg, StringComparison.OrdinalIgnoreCase);
			Assert.Contains(TemporaryLimitMeshColors.FluorescentOrange, svg, StringComparison.OrdinalIgnoreCase);
			Assert.Contains("mesh-temp-limits", svg, StringComparison.Ordinal);
			Assert.Contains("V temporal", svg, StringComparison.Ordinal);
		}

		[Fact]
		public void RenderContent_TemporaryLimitsHidden_OmitsBandsAndTempColors()
		{
			(Mesh mesh, RouteView view) = BuildMeshWithTemps();

			MeshSvgDrawOptions options = MeshSvgDrawOptions.Create(showTemporaryLimits: false);
			string svg = MeshSvgRenderer.RenderContent(
				mesh, view,
				TimeSpan.FromHours(6), TimeSpan.FromHours(10),
				0, 20000,
				width: 800, height: 500,
				options);

			Assert.DoesNotContain("mesh-temp-limits", svg, StringComparison.Ordinal);
			Assert.DoesNotContain("V temporal", svg, StringComparison.Ordinal);
			Assert.DoesNotContain(TemporaryLimitMeshColors.Yellow, svg, StringComparison.OrdinalIgnoreCase);
			Assert.DoesNotContain(TemporaryLimitMeshColors.FluorescentOrange, svg, StringComparison.OrdinalIgnoreCase);
		}

		[Fact]
		public void RouteView_GetTemporarySpeedLimit_MapsAxisPk()
		{
			(_, RouteView view) = BuildMeshWithTemps();
			Assert.Equal(80, view.GetTemporarySpeedLimit(3000L));
			Assert.Equal(30, view.GetTemporarySpeedLimit(12000L));
			Assert.Null(view.GetTemporarySpeedLimit(19000L));
		}

		private static (Mesh Mesh, RouteView View) BuildMeshWithTemps()
		{
			Station stA = new Station("A");
			stA.Name = "A";
			stA.Avr = "A";
			Station stB = new Station("B");
			stB.Name = "B";
			stB.Avr = "B";

			Axis axis = new Axis();
			axis.Id = "X1";
			axis.Vmax = 100;
			AxisVertex v0 = new AxisVertex(39.0, 2.0, 0L);
			v0.Station = stA;
			AxisVertex v1 = new AxisVertex(39.1, 2.1, 20000L);
			v1.Station = stB;
			axis.AddVertex(v0);
			axis.AddVertex(v1);
			axis.Rebuild();
			axis.SetCantonFrontiers(new long[] { 0L, 20000L });
			axis.DefaultTrackCount = 2;
			axis.FixedLimits.Add(90, 0L, 20000L);
			axis.TemporaryLimits.Add(80, 2000L, 5000L);
			axis.TemporaryLimits.Add(30, 10000L, 14000L);

			TopoLayout topo = new TopoLayout();
			topo.AddStation(stA);
			topo.AddStation(stB);
			topo.AddAxis(axis);

			Plan plan = new Plan(topo);
			plan.EnsureDefaultTrainSpecs();
			plan.DemandScript = "req A -> B 06:00-08:00 as R1\n  days lab\n";
			Assert.True(plan.CompileDemand().Success);
			Mesh mesh = new MeshPlanner(plan).Solve(DayOfWeek.Monday);
			Assert.NotEmpty(mesh.Circulations);
			return (mesh, RouteView.FromAxis(axis));
		}
	}
}
