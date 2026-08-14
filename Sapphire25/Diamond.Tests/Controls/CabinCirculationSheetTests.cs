using Diamond.Controls.Rendering;
using Diamond.Project;
using Diamond.Timed;
using Diamond.Topo;

namespace Diamond.Tests.Controls
{
	public class CabinCirculationSheetTests
	{
		[Fact]
		public void BuildFromProject_PalmaManacor_HasFrontiersAndOfficialTimes()
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
			Diamond.Project.Project project = ProjectCompiler.Compile(mesh);
			Assert.True(project.Circulations.Count > 0);

			Diamond.Project.Circulation? pc = null;
			int i = 0;
			while (i < project.Circulations.Count)
			{
				Diamond.Project.Circulation x = project.Circulations[i];
				if (string.Equals(x.Origin.Avr, "PMI", StringComparison.OrdinalIgnoreCase)
					&& string.Equals(x.Destination.Avr, "MAN", StringComparison.OrdinalIgnoreCase))
				{
					pc = x;
					break;
				}

				i++;
			}

			Assert.NotNull(pc);

			CirculationSheetDocument doc = CirculationSheetDocument.BuildFromProject(
				pc!,
				topo,
				editionLabel: "Tourmaline test",
				serviceDays: ServiceDays.FromDayOfWeekMask(DayOfWeek.Monday));

			Assert.True(doc.Frontiers.Count >= 10, "frontiers=" + doc.Frontiers.Count);
			Assert.True(doc.Pages.Count >= 1);
			Assert.False(string.IsNullOrEmpty(doc.TrainNumber));

			CirculationSheetFrontier origin = doc.Frontiers[0];
			Assert.True(origin.IsOrigin);
			Assert.Equal(pc!.Departure, origin.Departure);

			IReadOnlyList<string> daySheets = CirculationSheetSvgRenderer.RenderAllPages(
				doc, CirculationSheetPalette.CabinDay);
			Assert.True(daySheets.Count >= 1);
			Assert.Contains("fill=\"#ffffff\"", daySheets[0], StringComparison.Ordinal);
			Assert.Contains("#123a6b", daySheets[0], StringComparison.Ordinal);
			Assert.Contains("fill=\"#000000\"", daySheets[0], StringComparison.Ordinal);

			IReadOnlyList<string> nightSheets = CirculationSheetSvgRenderer.RenderAllPages(
				doc, CirculationSheetPalette.CabinNight);
			Assert.True(nightSheets.Count >= 1);
			Assert.Contains("fill=\"#000000\"", nightSheets[0], StringComparison.Ordinal);
			Assert.Contains("#c9a27a", nightSheets[0], StringComparison.Ordinal);
			Assert.Contains("#e07070", nightSheets[0], StringComparison.Ordinal);
		}

		[Fact]
		public void CabinDayAndNight_Palettes_MatchRequestedColors()
		{
			CirculationSheetPalette day = CirculationSheetPalette.CabinDay;
			Assert.Equal("#ffffff", day.Background);
			Assert.Equal("#000000", day.Text);
			Assert.Equal("#123a6b", day.Stroke);

			CirculationSheetPalette night = CirculationSheetPalette.CabinNight;
			Assert.Equal("#000000", night.Background);
			Assert.Equal("#e07070", night.Text);
			Assert.Equal("#c9a27a", night.Stroke);
		}
	}
}
