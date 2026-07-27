using Diamond.Timed;
using Diamond.Topo;

namespace Diamond.Tests.Timed
{
	public class DemandScriptParserTests
	{
		private const string SampleScript = """
			plan "SFM laborables"
			# comentario
			require 2/h  PMI -> MAN  06:00-22:00  as R-Palma-Manacor
			require both ways 1/h PMI -> SPB from 06:00 to 21:00
			require every 15 min PMI -> UIB using default as R-Metro
			""";

		[Fact]
		public void Parse_SampleScript_IsDeterministicAndOrdered()
		{
			DemandCompileResult a = DemandScriptParser.Parse(SampleScript);
			DemandCompileResult b = DemandScriptParser.Parse(SampleScript);

			Assert.True(a.Success);
			Assert.Equal("SFM laborables", a.PlanName);
			Assert.Equal(3, a.Requirements.Count);

			Assert.Equal("R-Palma-Manacor", a.Requirements[0].Id);
			Assert.Equal("PMI", a.Requirements[0].From.Text);
			Assert.Equal("MAN", a.Requirements[0].To.Text);
			Assert.Equal(2, a.Requirements[0].Frequency.TrainsPerHour);
			Assert.Equal(new TimeOnly(6, 0), a.Requirements[0].WindowStart);
			Assert.Equal(new TimeOnly(22, 0), a.Requirements[0].WindowEnd);
			Assert.Equal(DemandDirection.Forward, a.Requirements[0].Direction);

			Assert.Equal("R1", a.Requirements[1].Id); // auto-id
			Assert.Equal(DemandDirection.BothWays, a.Requirements[1].Direction);
			Assert.Equal(new TimeOnly(6, 0), a.Requirements[1].WindowStart);
			Assert.Equal(new TimeOnly(21, 0), a.Requirements[1].WindowEnd);

			Assert.Equal("R-Metro", a.Requirements[2].Id);
			Assert.Equal(15, a.Requirements[2].Frequency.IntervalMinutes);
			Assert.Equal(4.0, a.Requirements[2].Frequency.TrainsPerHourValue, 5);
			Assert.Equal("default", a.Requirements[2].FleetId);

			// Determinismo: mismos ids y mismos textos en el mismo orden.
			Assert.Equal(a.Requirements.Count, b.Requirements.Count);
			int index = 0;
			while (index < a.Requirements.Count)
			{
				Assert.Equal(a.Requirements[index].Id, b.Requirements[index].Id);
				Assert.Equal(a.Requirements[index].From.Text, b.Requirements[index].From.Text);
				Assert.Equal(a.Requirements[index].To.Text, b.Requirements[index].To.Text);
				Assert.Equal(a.Requirements[index].Frequency.ToString(), b.Requirements[index].Frequency.ToString());
				index++;
			}
		}

		[Fact]
		public void Parse_UnknownKeyword_ReportsError()
		{
			DemandCompileResult result = DemandScriptParser.Parse("foo 1/h A -> B");
			Assert.False(result.Success);
			Assert.Contains("desconocida", result.Errors[0], StringComparison.OrdinalIgnoreCase);
		}

		[Fact]
		public void Plan_CompileDemand_ResolvesStationsOnSfm()
		{
			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			Plan plan = new Plan(topo);
			plan.EnsureDefaultTrainSpecs();

			string script = """
				plan "demo"
				require 2/h PMI -> MAN 06:00-22:00 as R1
				require 4/h PMI -> UIB as R2
				""";

			DemandCompileResult result = plan.CompileDemand(script);
			Assert.True(result.Success, string.Join("; ", result.Errors));
			Assert.Equal(2, plan.Demand.Count);
			Assert.True(plan.Demand[0].IsResolved);
			Assert.Equal("Palma", plan.Demand[0].FromStation!.Name);
			Assert.Equal("Manacor", plan.Demand[0].ToStation!.Name);
			Assert.Equal("UIB", plan.Demand[1].ToStation!.Name);
		}

		[Fact]
		public void Plan_CompileDemand_UnknownStation_FailsResolve()
		{
			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			Plan plan = new Plan(topo);

			DemandCompileResult result = plan.CompileDemand("require 1/h PMI -> ZZ_NO_EXISTE");
			Assert.False(result.Success);
			Assert.Contains("desconocida", string.Join(" ", result.Errors), StringComparison.OrdinalIgnoreCase);
		}

		[Fact]
		public void Parse_SfmStyleStopPattern_AndEvery40Min()
		{
			string script = """
				require both ways every 40 min PMI -> MAN 06:00-22:00 as R-T3
				  stops 30s
				  skip RLL Enllaç "Sant Joan" PSJ
				  dwell INC 1min
				  cross at Petra
				""";

			DemandCompileResult result = DemandScriptParser.Parse(script);
			Assert.True(result.Success, string.Join("; ", result.Errors));
			Assert.Single(result.Requirements);

			DemandRequirement r = result.Requirements[0];
			Assert.Equal(40, r.Frequency.IntervalMinutes);
			Assert.Equal(DemandDirection.BothWays, r.Direction);
			Assert.Equal(TimeSpan.FromSeconds(30), r.Stops.DefaultDwell);
			Assert.Equal(4, r.Stops.Skip.Count);
			Assert.Single(r.Stops.Overrides);
			Assert.Equal("INC", r.Stops.Overrides[0].Station.Text);
			Assert.Equal(TimeSpan.FromMinutes(1), r.Stops.Overrides[0].Dwell);
			Assert.NotNull(r.Stops.CrossAt);
			Assert.Equal("Petra", r.Stops.CrossAt!.Text);
		}
	}
}
