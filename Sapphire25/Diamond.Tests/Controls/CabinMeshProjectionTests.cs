using Diamond.Controls.Rendering.CabinMesh;
using Diamond.Project;
using Diamond.Timed;
using Diamond.Topo;

namespace Diamond.Tests.Controls
{
	public class CabinMeshProjectionTests
	{
		[Fact]
		public void T3Train_AppearsOnPalmaSpbView_WithNumber()
		{
			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			SfmDemoInfrastructure.Apply(topo);
			Plan plan = new Plan(topo);
			plan.EnsureDefaultTrainSpecs();
			string script = """
				require both ways every 60 min PMI -> MAN 06:00-10:00 as R-T3
				  days lab
				  stops 30s
				require both ways every 60 min PMI -> SPB 06:00-10:00 as R-SPB
				  days lab
				  stops 30s
				""";
			Assert.True(plan.CompileDemand(script).Success);
			Mesh mesh = new MeshPlanner(plan).Solve(DayOfWeek.Monday);
			Diamond.Project.Project project = ProjectCompiler.Compile(mesh);
			Assert.True(project.Circulations.Count >= 4);

			RouteView ui = BuildPalmaSpb(topo);
			CabinMeshLayout layout = new CabinMeshLayout(400, 600, 5000, true, TimeSpan.FromHours(7));
			CabinMeshSvgBuilder.Result result = CabinMeshSvgBuilder.Build(
				layout,
				CabinMeshPalette.Day,
				ui,
				project.Circulations,
				active: null,
				nightMode: false,
				activeTrainVmaxKmh: 100,
				topo: topo);

			Assert.False(string.IsNullOrEmpty(result.SvgMarkup));
			int pathCount = CountOccurrences(result.SvgMarkup, "<path ");
			Assert.True(pathCount >= 2, "paths=" + pathCount);

			int numCount = CountOccurrences(result.SvgMarkup, "cabin-mesh-train-num");
			Assert.True(numCount >= 2, "numbers=" + numCount);
		}

		private static RouteView BuildPalmaSpb(TopoLayout topo)
		{
			Axis t3 = topo.FindAxisById("T3")!;
			Axis t2 = topo.FindAxisById("T2")!;
			StationOnAxis palma = t3.Stations.First(s =>
				string.Equals(s.Station.Avr, "PMI", StringComparison.OrdinalIgnoreCase));
			StationOnAxis enT3 = t3.Stations.First(s =>
				(s.Station.Name ?? string.Empty).Contains("Enlla", StringComparison.OrdinalIgnoreCase));
			StationOnAxis enT2 = t2.Stations.First(s =>
				(s.Station.Name ?? string.Empty).Contains("Enlla", StringComparison.OrdinalIgnoreCase));
			StationOnAxis spb = t2.Stations.First(s =>
				string.Equals(s.Station.Avr, "SPB", StringComparison.OrdinalIgnoreCase));
			List<(Axis, long, long)> segs = new List<(Axis, long, long)>();
			segs.Add((t3, palma.PK, enT3.PK));
			segs.Add((t2, enT2.PK, spb.PK));
			return RouteView.Concat("T3+T2", "Palma → Sa Pobla", segs);
		}

		private static int CountOccurrences(string text, string needle)
		{
			int n = 0;
			int i = 0;
			while (i >= 0)
			{
				i = text.IndexOf(needle, i, StringComparison.Ordinal);
				if (i < 0)
				{
					break;
				}

				n++;
				i += needle.Length;
			}

			return n;
		}
	}
}
