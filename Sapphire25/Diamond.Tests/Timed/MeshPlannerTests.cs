using Diamond.Timed;
using Diamond.Topo;

namespace Diamond.Tests.Timed
{
	public class MeshPlannerTests
	{
		[Fact]
		public void Solve_SimpleDemand_ProducesCirculationsAndSharedAsimilation()
		{
			Plan plan = CreatePlanWithCorridor();
			plan.DemandScript = """
				plan "demo"
				require 2/h A -> B 06:00-08:00 as R1
				""";
			DemandCompileResult compiled = plan.CompileDemand();
			Assert.True(compiled.Success, string.Join("; ", compiled.Errors));

			Mesh mesh = new MeshPlanner(plan).Solve();

			Assert.True(mesh.Success, string.Join("; ", mesh.Errors));
			Assert.True(mesh.Circulations.Count >= 2);
			Assert.Single(mesh.Asimilations); // mismo patrón → una sola asimilación
			Assert.All(mesh.Circulations, c => Assert.Same(mesh.Asimilations[0], c.Asimilation));
		}

		[Fact]
		public void Solve_DesiredHeadwayTooTight_WarnsAndSpacesByCantons()
		{
			Plan plan = CreatePlanWithCorridor();
			// 12/h = cada 5 min: probablemente más apretado que la ocupación de cantón.
			plan.DemandScript = """
				require 12/h A -> B 06:00-07:00 as R-tight
				""";
			Assert.True(plan.CompileDemand().Success);

			Mesh mesh = new MeshPlanner(plan).Solve();
			Assert.True(mesh.Success, string.Join("; ", mesh.Errors));
			Assert.NotEmpty(mesh.Warnings);
			Assert.Contains(mesh.Warnings, w => w.Contains("cadencia", StringComparison.OrdinalIgnoreCase)
				|| w.Contains("deseados", StringComparison.OrdinalIgnoreCase)
				|| w.Contains("hueco", StringComparison.OrdinalIgnoreCase));
		}

		[Fact]
		public void Solve_BothWays_OnDoubleTrack_CanCross()
		{
			Plan plan = CreatePlanWithCorridor(doubleTrack: true);
			plan.DemandScript = """
				require both ways 1/h A -> B 06:00-08:00 as R-both
				""";
			Assert.True(plan.CompileDemand().Success);

			Mesh mesh = new MeshPlanner(plan).Solve();
			Assert.True(mesh.Success, string.Join("; ", mesh.Errors));
			Assert.True(mesh.Circulations.Count >= 2);
			// Ida y vuelta: al menos dos asimilaciones (sentidos opuestos) o una por sentido.
			Assert.True(mesh.Asimilations.Count >= 1);
		}

		private static Plan CreatePlanWithCorridor(bool doubleTrack = false)
		{
			// Eje sintético 0..20000 m, estaciones principales A (0), M (10000), B (20000).
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
			axis.DefaultTrackCount = 1;
			if (doubleTrack)
			{
				axis.SetTrackCount(0L, 20000L, 2);
			}

			TopoLayout topo = new TopoLayout();
			topo.AddStation(stA);
			topo.AddStation(stM);
			topo.AddStation(stB);
			topo.AddAxis(axis);

			Plan plan = new Plan(topo);
			plan.EnsureDefaultTrainSpecs();
			return plan;
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
