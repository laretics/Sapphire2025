using Diamond.Timed;
using Diamond.Topo;

namespace Diamond.Tests.Timed
{
	public class DemandAsimilationDefTests
	{
		[Fact]
		public void Parse_AsimWithNumbersAndColor_UnderDaysRegion()
		{
			string script = """
				plan "SFM"
				days lab
				  asim PMI -> MAN numbers 49## color #38bdf8
				  asim PMI -> SPB
				    numbers 47##
				    color orange
				req both ways every 60 min PMI -> MAN 06:00-10:00 as R-T3
				""";

			DemandCompileResult result = DemandScriptParser.Parse(script);
			Assert.True(result.Success, string.Join("; ", result.Errors));
			Assert.Equal(2, result.AsimilationDefs.Count);

			DemandAsimilationDef man = result.AsimilationDefs[0];
			Assert.Equal("PMI", man.From.Text);
			Assert.Equal("MAN", man.To.Text);
			Assert.Equal("49##", man.NumberPattern);
			Assert.Equal("#38bdf8", man.Color);
			Assert.True(man.Days.AppliesOn(DayOfWeek.Monday));
			Assert.False(man.Days.AppliesOn(DayOfWeek.Sunday));

			DemandAsimilationDef spb = result.AsimilationDefs[1];
			Assert.Equal("47##", spb.NumberPattern);
			Assert.Equal("#ffa500", spb.Color);
		}

		[Fact]
		public void Parse_AsimAlphanumericPattern_PHashMtx()
		{
			string script = """
				days lab
				  asim PMI -> MTX numbers P##MTX color #fbbf24
				""";

			DemandCompileResult result = DemandScriptParser.Parse(script);
			Assert.True(result.Success, string.Join("; ", result.Errors));
			Assert.Single(result.AsimilationDefs);
			Assert.Equal("P##MTX", result.AsimilationDefs[0].NumberPattern);
			Assert.Equal("P1MTX", TrainNumbering.ExpandPattern("P##MTX", 1));
			Assert.Equal("P3MTX", TrainNumbering.ExpandPattern("P##MTX", 3));
			Assert.Equal("P2MTX", TrainNumbering.ExpandPattern("P##MTX", 2));
			Assert.Equal("4901", TrainNumbering.ExpandPattern("49##", 1));
			Assert.Equal("4902", TrainNumbering.ExpandPattern("49##", 2));
		}

		[Fact]
		public void Parse_AsimFestivos_DifferentSeries()
		{
			string script = """
				days lab
				  asim PMI -> MAN numbers 49##
				days fes
				  asim PMI -> MAN numbers 44## color #f472b6
				""";

			DemandCompileResult result = DemandScriptParser.Parse(script);
			Assert.True(result.Success, string.Join("; ", result.Errors));
			Assert.Equal(2, result.AsimilationDefs.Count);
			Assert.Equal("49##", result.AsimilationDefs[0].NumberPattern);
			Assert.True(result.AsimilationDefs[0].Days.AppliesOn(DayOfWeek.Tuesday));
			Assert.Equal("44##", result.AsimilationDefs[1].NumberPattern);
			Assert.True(result.AsimilationDefs[1].Days.AppliesOn(DayOfWeek.Sunday));
			Assert.False(result.AsimilationDefs[1].Days.AppliesOn(DayOfWeek.Monday));
		}

		[Fact]
		public void Parse_AsimWithoutNumbersOrColor_IsError()
		{
			DemandCompileResult result = DemandScriptParser.Parse("asim PMI -> MAN\n");
			Assert.False(result.Success);
			Assert.Contains(result.Errors, e => e.IndexOf("numbers", StringComparison.OrdinalIgnoreCase) >= 0
				|| e.IndexOf("color", StringComparison.OrdinalIgnoreCase) >= 0);
		}

		[Fact]
		public void TryParseNumberPattern_AcceptsCommonForms()
		{
			string pattern;
			string? err;
			Assert.True(TrainNumbering.TryParseNumberPattern("49##", out pattern, out err));
			Assert.Equal("49##", pattern);
			Assert.True(TrainNumbering.TryParseNumberPattern("P##MTX", out pattern, out err));
			Assert.Equal("P##MTX", pattern);
			Assert.True(TrainNumbering.TryParseNumberPattern("47xx", out pattern, out err));
			Assert.Equal("47##", pattern);
			Assert.True(TrainNumbering.TryParseNumberPattern("45", out pattern, out err));
			Assert.Equal("45##", pattern);
			Assert.True(TrainNumbering.TryParseNumberPattern("4900", out pattern, out err));
			Assert.Equal("49##", pattern);
			Assert.False(TrainNumbering.TryParseNumberPattern("abc", out pattern, out err));
		}

		[Fact]
		public void Numbering_UsesScriptSeries_ForMatchingCorridor()
		{
			TopoLayout topo = DemoInfrastructure();
			string script = """
				plan "num"
				days lab
				  asim PMI -> MAN numbers 49## color #112233
				  asim MAN -> PMI numbers 49## color #112233
				require both ways every 60 min PMI -> MAN 06:00-08:00 as R1
				  days lab
				  stops 30s
				""";

			Plan plan = new Plan(topo);
			plan.EnsureDefaultTrainSpecs();
			DemandCompileResult compiled = plan.CompileDemand(script);
			Assert.True(compiled.Success, string.Join("; ", compiled.Errors));
			Assert.Equal(2, plan.AsimilationDefs.Count);

			Mesh mesh = new MeshPlanner(plan).Solve(DayOfWeek.Monday);
			Assert.True(mesh.Circulations.Count >= 2);

			int i = 0;
			while (i < mesh.Circulations.Count)
			{
				Circulation c = mesh.Circulations[i];
				Assert.True(c.HasServiceNumber);
				Assert.StartsWith("49", c.ServiceNumber);
				Assert.True(c.HasColor);
				Assert.Equal("#112233", c.Color);
				i++;
			}
		}

		[Fact]
		public void Numbering_DirectedAsim_DifferentPatternsPerDirection()
		{
			TopoLayout topo = DemoInfrastructure();
			string script = """
				plan "dir"
				days lab
				  asim PMI -> MAN numbers 49##
				  asim MAN -> PMI numbers 48##
				require both ways every 60 min PMI -> MAN 06:00-08:00 as R1
				  days lab
				  stops 30s
				""";

			Plan plan = new Plan(topo);
			plan.EnsureDefaultTrainSpecs();
			Assert.True(plan.CompileDemand(script).Success);

			Mesh mesh = new MeshPlanner(plan).Solve(DayOfWeek.Monday);
			List<Circulation> toMan = mesh.Circulations
				.Where(c => string.Equals(c.Asimilation.Destination.Station.Avr, "MAN", StringComparison.OrdinalIgnoreCase))
				.ToList();
			List<Circulation> toPmi = mesh.Circulations
				.Where(c => string.Equals(c.Asimilation.Destination.Station.Avr, "PMI", StringComparison.OrdinalIgnoreCase))
				.ToList();

			Assert.NotEmpty(toMan);
			Assert.NotEmpty(toPmi);
			Assert.All(toMan, c => Assert.StartsWith("49", c.ServiceNumber));
			Assert.All(toPmi, c => Assert.StartsWith("48", c.ServiceNumber));
			Assert.All(toMan, c =>
			{
				int n = int.Parse(c.ServiceNumber, System.Globalization.CultureInfo.InvariantCulture);
				Assert.Equal(1, n % 2);
			});
			Assert.All(toPmi, c =>
			{
				int n = int.Parse(c.ServiceNumber, System.Globalization.CultureInfo.InvariantCulture);
				Assert.Equal(0, n % 2);
			});
		}

		[Fact]
		public void Numbering_DirectedAsim_SamePattern_OddAscendingEvenDescending()
		{
			TopoLayout topo = DemoInfrastructure();
			string script = """
				plan "weekend"
				days fes
				  asim PMI -> SPB numbers 48## color #30e3e3
				  asim SPB -> PMI numbers 48## color #30e3e3
				  asim PMI -> MAN numbers 44## color #e3e330
				  asim MAN -> PMI numbers 44## color #e3e330
				req 1/h PMI -> SPB 06:35-09:40 as R-spb-up
				  days fes
				  stops 30s
				req 1/h SPB -> PMI 08:08-11:09 as R-spb-down
				  days fes
				  stops 30s
				req 1/h PMI -> MAN 06:04-09:05 as R-man-up
				  days fes
				  stops 30s
				req 1/h MAN -> PMI 06:25-09:25 as R-man-down
				  days fes
				  stops 30s
				""";

			Plan plan = new Plan(topo);
			plan.EnsureDefaultTrainSpecs();
			DemandCompileResult compiled = plan.CompileDemand(script);
			Assert.True(compiled.Success, string.Join("; ", compiled.Errors));

			Mesh mesh = new MeshPlanner(plan).Solve(DayOfWeek.Sunday);
			Assert.True(mesh.Circulations.Count >= 4);

			List<Circulation> toSpb = mesh.Circulations
				.Where(c => DestinationAvr(c, "SPB"))
				.OrderBy(c => c.Departure)
				.ToList();
			List<Circulation> fromSpb = mesh.Circulations
				.Where(c => OriginAvr(c, "SPB"))
				.OrderBy(c => c.Departure)
				.ToList();
			List<Circulation> toMan = mesh.Circulations
				.Where(c => DestinationAvr(c, "MAN"))
				.OrderBy(c => c.Departure)
				.ToList();
			List<Circulation> fromMan = mesh.Circulations
				.Where(c => OriginAvr(c, "MAN"))
				.OrderBy(c => c.Departure)
				.ToList();

			Assert.NotEmpty(toSpb);
			Assert.NotEmpty(fromSpb);
			Assert.NotEmpty(toMan);
			Assert.NotEmpty(fromMan);

			Assert.All(toSpb, c => AssertParity(c, series: 48, odd: true));
			Assert.All(fromSpb, c => AssertParity(c, series: 48, odd: false));
			Assert.All(toMan, c => AssertParity(c, series: 44, odd: true));
			Assert.All(fromMan, c => AssertParity(c, series: 44, odd: false));

			AssertSequentialStep(toSpb, 2);
			AssertSequentialStep(fromSpb, 2);
			AssertSequentialStep(toMan, 2);
			AssertSequentialStep(fromMan, 2);
		}

		[Fact]
		public void Numbering_DirectedAsim_OneDirectionOnly_StillOddIfAscending()
		{
			TopoLayout topo = DemoInfrastructure();
			string script = """
				plan "solo-ida"
				days lab
				  asim PMI -> MAN numbers 44##
				require every 60 min PMI -> MAN 06:00-08:00 as R-up
				  days lab
				  stops 30s
				""";

			Plan plan = new Plan(topo);
			plan.EnsureDefaultTrainSpecs();
			Assert.True(plan.CompileDemand(script).Success);

			Mesh mesh = new MeshPlanner(plan).Solve(DayOfWeek.Monday);
			Assert.NotEmpty(mesh.Circulations);
			Assert.All(mesh.Circulations, c => AssertParity(c, series: 44, odd: true));
			AssertSequentialStep(
				mesh.Circulations.OrderBy(c => c.Departure).ToList(),
				2);
		}

		private static bool OriginAvr(Circulation circulation, string avr)
		{
			return string.Equals(
				circulation.Asimilation.Origin.Station.Avr,
				avr,
				StringComparison.OrdinalIgnoreCase);
		}

		private static bool DestinationAvr(Circulation circulation, string avr)
		{
			return string.Equals(
				circulation.Asimilation.Destination.Station.Avr,
				avr,
				StringComparison.OrdinalIgnoreCase);
		}

		private static void AssertParity(Circulation circulation, int series, bool odd)
		{
			Assert.True(circulation.HasServiceNumber);
			int n = int.Parse(circulation.ServiceNumber, System.Globalization.CultureInfo.InvariantCulture);
			Assert.Equal(series, n / 100);
			Assert.Equal(odd ? 1 : 0, n % 2);
		}

		private static void AssertSequentialStep(List<Circulation> ordered, int step)
		{
			int i = 1;
			while (i < ordered.Count)
			{
				int prev = int.Parse(ordered[i - 1].ServiceNumber, System.Globalization.CultureInfo.InvariantCulture);
				int cur = int.Parse(ordered[i].ServiceNumber, System.Globalization.CultureInfo.InvariantCulture);
				Assert.Equal(prev + step, cur);
				i++;
			}
		}

		private static TopoLayout DemoInfrastructure()
		{
			string[] candidates = new[]
			{
				Path.Combine(AppContext.BaseDirectory, "Samples", "Onice", "toposfm227.xml"),
				Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Samples", "Onice", "toposfm227.xml")),
				Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Diamond.Web", "Samples", "Onice", "toposfm227.xml")),
			};

			int index = 0;
			while (index < candidates.Length)
			{
				if (File.Exists(candidates[index]))
				{
					TopoLayout topo = TopoXmlSerializer.Load(candidates[index]);
					SfmDemoInfrastructure.Apply(topo);
					return topo;
				}

				index++;
			}

			throw new FileNotFoundException("No se encontró toposfm227.xml para el test de asimilación.");
		}
	}
}
