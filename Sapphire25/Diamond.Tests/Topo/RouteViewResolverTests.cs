using Diamond.Project;
using Diamond.Timed;
using Diamond.Topo;
using Diamond.Controls.Rendering;

namespace Diamond.Tests.Topo
{
	public class RouteViewResolverTests
	{
		[Fact]
		public void TryFromViewId_PathSignature_RebuildsPalmaSpb()
		{
			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			SfmDemoInfrastructure.Apply(topo);
			RouteView catalog = BuildPalmaSpb(topo);
			string sig = catalog.PathSignature();

			RouteView? rebuilt = RouteViewResolver.TryFromViewId(topo, sig);
			Assert.NotNull(rebuilt);
			Assert.Equal(2, rebuilt!.Legs.Count);
			Assert.Equal(sig, rebuilt.PathSignature());
			Assert.NotNull(rebuilt.FindStationByRef("01", "PMI", "Palma"));
			Assert.NotNull(rebuilt.FindStationByRef("33", "SPB", "Sa Pobla"));
		}

		[Fact]
		public void TryFromViewId_BareComposite_DoesNotConcatFullAxes()
		{
			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			SfmDemoInfrastructure.Apply(topo);
			Assert.Null(RouteViewResolver.TryFromViewId(topo, "T3+T2"));
		}

		[Fact]
		public void TryFromViewId_SingleAxis_Works()
		{
			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			SfmDemoInfrastructure.Apply(topo);
			RouteView? t3 = RouteViewResolver.TryFromViewId(topo, "T3");
			Assert.NotNull(t3);
			Assert.Equal("T3", t3!.Id);
			Assert.Single(t3.Legs);
		}

		[Fact]
		public void BuildFromProject_PalmaSpb_UsesPathSignature()
		{
			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			SfmDemoInfrastructure.Apply(topo);
			Plan plan = new Plan(topo);
			plan.EnsureDefaultTrainSpecs();
			string script = """
				require both ways every 60 min PMI -> SPB 06:00-10:00 as R-SPB
				  days lab
				  stops 30s
				""";
			Assert.True(plan.CompileDemand(script).Success);
			Mesh mesh = new MeshPlanner(plan).Solve(DayOfWeek.Monday);
			Diamond.Project.Project project = ProjectCompiler.Compile(mesh);
			Diamond.Project.Circulation? pc = null;
			int i = 0;
			while (i < project.Circulations.Count)
			{
				Diamond.Project.Circulation x = project.Circulations[i];
				if (string.Equals(x.Origin.Avr, "PMI", StringComparison.OrdinalIgnoreCase)
					&& string.Equals(x.Destination.Avr, "SPB", StringComparison.OrdinalIgnoreCase))
				{
					pc = x;
					break;
				}

				i++;
			}

			Assert.NotNull(pc);
			Assert.Contains('+', pc!.Asimilation.ViewId + pc.Asimilation.PathSignature);

			CirculationSheetDocument doc = CirculationSheetDocument.BuildFromProject(pc, topo);
			Assert.True(doc.Frontiers.Count >= 5, "frontiers=" + doc.Frontiers.Count);
			bool hasSpb = false;
			int f = 0;
			while (f < doc.Frontiers.Count)
			{
				if (doc.Frontiers[f].DependencyName.Contains("POBLA", StringComparison.OrdinalIgnoreCase)
					|| doc.Frontiers[f].IsDestination)
				{
					hasSpb = true;
					break;
				}

				f++;
			}

			Assert.True(hasSpb, "Sa Pobla missing from 47xx sheet");
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
	}
}
