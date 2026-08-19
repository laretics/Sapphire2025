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
			Assert.Contains("Bloq.", daySheets[0], StringComparison.Ordinal);
			Assert.Contains("BAU", daySheets[0], StringComparison.Ordinal);
			Assert.Contains("BAB", daySheets[0], StringComparison.Ordinal);
			Assert.DoesNotContain(">Vía<", daySheets[0], StringComparison.Ordinal);

			IReadOnlyList<string> nightSheets = CirculationSheetSvgRenderer.RenderAllPages(
				doc, CirculationSheetPalette.CabinNight);
			Assert.True(nightSheets.Count >= 1);
			Assert.Contains("fill=\"#000000\"", nightSheets[0], StringComparison.Ordinal);
			Assert.Contains("#c9a27a", nightSheets[0], StringComparison.Ordinal);
			Assert.Contains("#e07070", nightSheets[0], StringComparison.Ordinal);

			AssertTrackTypesPalmaManacor(doc);
		}

		[Fact]
		public void BuildFromProject_PalmaSpb_HasBabThenBau_AndTempsOnT3AndT2()
		{
			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			SfmDemoInfrastructure.Apply(topo);
			Axis t3 = topo.FindAxisById("T3")!;
			Axis t2 = topo.FindAxisById("T2")!;
			long enllacT3 = t3.Stations.First(s =>
				(s.Station.Name ?? string.Empty).Contains("Enlla", StringComparison.OrdinalIgnoreCase)).PK;
			StationOnAxis enT2 = t2.Stations.First(s =>
				(s.Station.Name ?? string.Empty).Contains("Enlla", StringComparison.OrdinalIgnoreCase));
			StationOnAxis spb = t2.Stations.First(s =>
				string.Equals(s.Station.Avr, "SPB", StringComparison.OrdinalIgnoreCase));
			long t2Mid = enT2.PK + ((spb.PK - enT2.PK) / 2);
			if (t2Mid == enT2.PK)
			{
				t2Mid = enT2.PK < spb.PK ? enT2.PK + 1 : enT2.PK - 1;
			}

			List<TemporarySpeedLimit> temps = new List<TemporarySpeedLimit>
			{
				TopoTemporaryLimits.FromSpan(
					"T3", 5000L, 6000L, 40, TemporaryLimitReason.Works, "T3-BAB"),
				TopoTemporaryLimits.FromSpan(
					"T2", Math.Min(t2Mid, t2Mid + 800L), Math.Max(t2Mid, t2Mid + 800L),
					35, TemporaryLimitReason.Geometry, "T2-BAU"),
				TopoTemporaryLimits.FromSpan(
					"T3", enllacT3 + 4000L, enllacT3 + 5000L, 30, TemporaryLimitReason.Works, "T3-MAN")
			};
			TopoTemporaryLimits.Apply(topo, temps);

			Plan plan = new Plan(topo);
			plan.EnsureDefaultTrainSpecs();
			Assert.True(plan.CompileDemand("""
				require both ways every 60 min PMI -> SPB 06:00-10:00 as R-SPB
				  days lab
				  stops 30s
				""").Success);
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
			CirculationSheetDocument doc = CirculationSheetDocument.BuildFromProject(
				pc!,
				topo,
				includeTemporaryLimits: true);

			int bab = CountOutgoing(doc, doubleTrack: true);
			int bau = CountOutgoing(doc, doubleTrack: false);
			Assert.True(bab > 0, "Palma–Enllaç en T3+T2 debe ser BAB; bab=" + bab);
			Assert.True(bau > 0, "T2 Enllaç–Sa Pobla debe ser BAU; bau=" + bau);

			int tempCount = 0;
			bool sawT3 = false;
			bool sawT2 = false;
			bool sawManacor = false;
			int f = 0;
			while (f < doc.Frontiers.Count)
			{
				CirculationSheetFrontier row = doc.Frontiers[f];
				f++;
				if (!row.OutgoingIsTemporary)
				{
					continue;
				}

				tempCount++;
				if (string.Equals(row.TemporaryObservations, "T3-BAB", StringComparison.Ordinal))
				{
					sawT3 = true;
				}

				if (string.Equals(row.TemporaryObservations, "T2-BAU", StringComparison.Ordinal))
				{
					sawT2 = true;
				}

				if (string.Equals(row.TemporaryObservations, "T3-MAN", StringComparison.Ordinal))
				{
					sawManacor = true;
				}
			}

			Assert.True(sawT3, "Falta la temporal de T3 (BAB) en la ficha T3+T2");
			Assert.True(sawT2, "Falta la temporal de T2 en la ficha T3+T2");
			Assert.False(sawManacor, "La temporal de T3 hacia Manacor no pertenece a Palma–SPB");
			Assert.True(tempCount >= 2, "temps=" + tempCount);
		}

		private static void AssertTrackTypesPalmaManacor(CirculationSheetDocument doc)
		{
			int doubleCount = CountOutgoing(doc, doubleTrack: true);
			int singleCount = CountOutgoing(doc, doubleTrack: false);
			Assert.True(doubleCount > 0, "Palma–Enllaç debe salir como doble vía; double=" + doubleCount);
			Assert.True(singleCount > 0, "Enllaç–Manacor debe salir como vía única; single=" + singleCount);
		}

		private static int CountOutgoing(CirculationSheetDocument doc, bool doubleTrack)
		{
			int count = 0;
			int i = 0;
			while (i < doc.Frontiers.Count)
			{
				CirculationSheetFrontier row = doc.Frontiers[i];
				i++;
				if (!row.OutgoingTrackCount.HasValue)
				{
					continue;
				}

				if (row.OutgoingIsDoubleTrack == doubleTrack)
				{
					count++;
				}
			}

			return count;
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
