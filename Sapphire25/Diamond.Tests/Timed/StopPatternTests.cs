using Diamond.Motion;
using Diamond.Timed;
using Diamond.Topo;

namespace Diamond.Tests.Timed
{
	public class StopPatternTests
	{
		[Fact]
		public void Matches_Inc_DoesNotMatchPontDInca()
		{
			// Regresión: IndexOf("INC") dentro de "Pont d'Inca" hinchaba dwell INC.
			Assert.True(StopPattern.Matches(new StationRef("INC"), "17", "INC", "Inca"));
			Assert.False(StopPattern.Matches(new StationRef("INC"), "08", "pdi", "Pont d'Inca"));
			Assert.False(StopPattern.Matches(new StationRef("INC"), "09", "pdn", "Pont d'Inca Nou"));
		}

		[Fact]
		public void Matches_ExactName_StillWorksForCompositeNames()
		{
			Assert.True(StopPattern.Matches(
				new StationRef("Pont d'Inca"), "08", "pdi", "Pont d'Inca"));
			Assert.True(StopPattern.Matches(
				new StationRef("pdi"), "08", "pdi", "Pont d'Inca"));
		}

		[Fact]
		public void Matches_AccentInsensitive_ExactName()
		{
			Assert.True(StopPattern.Matches(new StationRef("Enllac"), "19", "ELÇ", "Enllaç"));
			Assert.True(StopPattern.Matches(new StationRef("Enllaç"), "19", "ELÇ", "Enllaç"));
		}

		[Fact]
		public void TryGetDwell_IncOverride_OnlyAffectsInca()
		{
			StopPattern pattern = new StopPattern();
			pattern.DefaultDwell = TimeSpan.FromSeconds(30);
			pattern.AddOverride(new StationRef("INC"), TimeSpan.FromMinutes(5));

			TimeSpan dwell;
			Assert.True(pattern.TryGetDwell("17", "INC", "Inca", out dwell));
			Assert.Equal(TimeSpan.FromMinutes(5), dwell);

			Assert.True(pattern.TryGetDwell("08", "pdi", "Pont d'Inca", out dwell));
			Assert.Equal(TimeSpan.FromSeconds(30), dwell);

			Assert.True(pattern.TryGetDwell("09", "pdn", "Pont d'Inca Nou", out dwell));
			Assert.Equal(TimeSpan.FromSeconds(30), dwell);
		}

		[Fact]
		public void Mesh_DwellInc_DoesNotInflatePontDInca()
		{
			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			SfmDemoInfrastructure.Apply(topo);
			Plan plan = new Plan(topo);
			plan.EnsureDefaultTrainSpecs();
			plan.DemandScript = """
				days lab
				  req PMI -> MAN 06:00-22:00
				    stops 30s
				    dwell INC 5min
				""";
			Assert.True(plan.CompileDemand().Success, string.Join("; ", plan.CompileDemand().Errors));

			Mesh mesh = new MeshPlanner(plan).Solve(DayOfWeek.Monday);
			Assert.NotEmpty(mesh.Circulations);
			Asimilation asim = mesh.Circulations[0].Asimilation;

			TimeSpan? inca = null;
			TimeSpan? pdi = null;
			TimeSpan? pdn = null;
			int i = 0;
			while (i < asim.Stops.Count)
			{
				AsimilationStop stop = asim.Stops[i];
				string avr = stop.Placement.Station.Avr ?? string.Empty;
				if (string.Equals(avr, "INC", StringComparison.OrdinalIgnoreCase))
				{
					inca = stop.Dwell;
				}
				else if (string.Equals(avr, "pdi", StringComparison.OrdinalIgnoreCase))
				{
					pdi = stop.Dwell;
				}
				else if (string.Equals(avr, "pdn", StringComparison.OrdinalIgnoreCase))
				{
					pdn = stop.Dwell;
				}

				i++;
			}

			Assert.Equal(TimeSpan.FromMinutes(5), inca);
			Assert.Equal(TimeSpan.FromSeconds(30), pdi);
			Assert.Equal(TimeSpan.FromSeconds(30), pdn);
		}
	}
}
