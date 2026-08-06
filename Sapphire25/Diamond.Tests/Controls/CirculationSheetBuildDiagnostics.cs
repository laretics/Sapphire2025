using Diamond.Controls.Rendering;
using Diamond.Timed;
using Diamond.Topo;

namespace Diamond.Tests.Controls
{
	public class CirculationSheetBuildDiagnostics
	{
		[Fact]
		public void PalmaManacor_Sheet_HasManyFrontiers_AndPagesHaveRows()
		{
			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			SfmDemoInfrastructure.Apply(topo);
			Plan plan = new Plan(topo);
			plan.EnsureDefaultTrainSpecs();
			string script = """
				require both ways every 40 min PMI -> MAN 06:00-10:00 as R-T3
				  days lab
				  stops 30s
				  skip RLL Enllaç "Sant Joan" PSJ
				  dwell INC 1min
				""";
			Assert.True(plan.CompileDemand(script).Success);
			Mesh mesh = new MeshPlanner(plan).Solve(DayOfWeek.Monday);
			Circulation? c = null;
			int i = 0;
			while (i < mesh.Circulations.Count)
			{
				Circulation x = mesh.Circulations[i];
				if (string.Equals(x.Asimilation.Origin.Station.Avr, "PMI", StringComparison.OrdinalIgnoreCase)
					&& string.Equals(x.Asimilation.Destination.Station.Avr, "MAN", StringComparison.OrdinalIgnoreCase))
				{
					c = x;
					break;
				}
				i++;
			}
			Assert.NotNull(c);

			CirculationSheetDocument doc = CirculationSheetDocument.Build(c!, mesh, 36);
			System.Console.WriteLine("frontiers=" + doc.Frontiers.Count + " pages=" + doc.Pages.Count);
			int p = 0;
			while (p < doc.Pages.Count)
			{
				CirculationSheetPage page = doc.Pages[p];
				System.Console.WriteLine("page " + page.PageNumber + " rows=" + page.Frontiers.Count
					+ " first=" + page.Frontiers[0].DependencyName
					+ " last=" + page.Frontiers[page.Frontiers.Count - 1].DependencyName);
				Assert.True(page.Frontiers.Count > 0, "page " + page.PageNumber + " empty");
				p++;
			}

			Assert.True(doc.Frontiers.Count >= 20, "too few frontiers: " + doc.Frontiers.Count);
			// Manacor must appear
			bool hasMan = false;
			int fi = 0;
			while (fi < doc.Frontiers.Count)
			{
				if (doc.Frontiers[fi].DependencyName.Contains("MANACOR", StringComparison.OrdinalIgnoreCase)
					|| doc.Frontiers[fi].IsDestination)
				{
					hasMan = true;
					break;
				}
				fi++;
			}
			Assert.True(hasMan, "Manacor missing from frontiers");

			// Mitades de libro: caben con MinRowH en A4 apaisado.
			int n0 = doc.Pages[0].Frontiers.Count;
			double avail = CirculationSheetSvgRenderer.AvailableBodyHeight;
			double bodyMin = n0 * 11.0;
			System.Console.WriteLine("page0 rows=" + n0 + " bodyMinH=" + bodyMin + " avail=" + avail
				+ " sheets=" + CirculationSheetPager.ComputeSheetCount(doc.Pages.Count));
			Assert.True(bodyMin <= avail + 1.0, "page0 content taller than half-page: " + bodyMin + " > " + avail);

			// 2 mitades → 1 hoja física A4 apaisada con ambas tablas.
			IReadOnlyList<string> sheets = CirculationSheetSvgRenderer.RenderAllPages(doc);
			Assert.Equal(CirculationSheetPager.ComputeSheetCount(doc.Pages.Count), sheets.Count);
			Assert.Contains("viewBox=\"0 0 841.89 595.28\"", sheets[0], StringComparison.Ordinal);
		}
	}
}
