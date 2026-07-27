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
		public void Parse_Req_IsAliasOfRequire()
		{
			string script = """
				plan "alias"
				req both ways every 40 min PMI -> MAN 06:00-22:00 as R-short
				  days lab
				  stops 30s
				""";

			DemandCompileResult result = DemandScriptParser.Parse(script);
			Assert.True(result.Success, string.Join("; ", result.Errors));
			Assert.Single(result.Requirements);
			Assert.Equal("R-short", result.Requirements[0].Id);
			Assert.Equal(DemandDirection.BothWays, result.Requirements[0].Direction);
			Assert.Equal(40, result.Requirements[0].Frequency.IntervalMinutes);
		}

		[Fact]
		public void Parse_OmittedFrequency_MeansOnce()
		{
			string script = """
				req PMI -> MAN 06:05-07:00 as R-one
				  days lab
				""";

			DemandCompileResult result = DemandScriptParser.Parse(script);
			Assert.True(result.Success, string.Join("; ", result.Errors));
			Assert.Single(result.Requirements);
			Assert.True(result.Requirements[0].Frequency.IsOnce);
			Assert.Equal("PMI", result.Requirements[0].From.Text);
			Assert.Equal("MAN", result.Requirements[0].To.Text);
			Assert.Equal(new TimeOnly(6, 5), result.Requirements[0].WindowStart);
		}

		[Fact]
		public void Parse_BothWays_OmittedFrequency_StillParses()
		{
			DemandCompileResult result = DemandScriptParser.Parse(
				"req both ways PMI -> SPB 05:35-06:30 as R-pair");
			Assert.True(result.Success, string.Join("; ", result.Errors));
			Assert.True(result.Requirements[0].Frequency.IsOnce);
			Assert.Equal(DemandDirection.BothWays, result.Requirements[0].Direction);
		}

		[Fact]
		public void Parse_SingleTime_ExpandsToOneHourWindow()
		{
			DemandCompileResult result = DemandScriptParser.Parse(
				"req PMI -> SPB 5:35 as R-one");
			Assert.True(result.Success, string.Join("; ", result.Errors));
			Assert.Equal(new TimeOnly(5, 35), result.Requirements[0].WindowStart);
			Assert.Equal(new TimeOnly(6, 35), result.Requirements[0].WindowEnd);
		}

		[Fact]
		public void Parse_SingleTime_LateEvening_ClampsEndTo2359()
		{
			DemandCompileResult result = DemandScriptParser.Parse(
				"req PMI -> MAN 23:30 as R-late");
			Assert.True(result.Success, string.Join("; ", result.Errors));
			Assert.Equal(new TimeOnly(23, 30), result.Requirements[0].WindowStart);
			Assert.Equal(new TimeOnly(23, 59), result.Requirements[0].WindowEnd);
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
			string script =
				"require both ways every 40 min PMI -> MAN 06:00-22:00 as R-T3\n"
				+ "  stops 30s\n"
				+ "  skip RLL Enllaç \"Sant Joan\" PSJ\n"
				+ "  dwell INC 1min\n"
				+ "  days lab\n"
				+ "  cross at Petra\n";

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
			Assert.True(r.AppliesOn(DayOfWeek.Monday));
			Assert.False(r.AppliesOn(DayOfWeek.Sunday));
			Assert.Equal(ServiceDay.Laborables, r.ServiceDays.Days);
			Assert.False(r.HasColor);
			Assert.Equal(string.Empty, r.Color);
		}

		[Fact]
		public void Parse_ColorContinuation_AndInline()
		{
			string script =
				"req PMI -> MAN 06:00-08:00 as R-pink\n"
				+ "  color #f472b6\n"
				+ "req PMI -> SPB 07:00-08:00 color #f00 as R-red\n"
				+ "req PMI -> INC 08:00-09:00 colour orange as R-named\n"
				+ "req PMI -> MAN 09:00-10:00 as R-auto\n";

			DemandCompileResult result = DemandScriptParser.Parse(script);
			Assert.True(result.Success, string.Join("; ", result.Errors));
			Assert.Equal(4, result.Requirements.Count);

			Assert.True(result.Requirements[0].HasColor);
			Assert.Equal("#f472b6", result.Requirements[0].Color);
			Assert.Equal("#ff0000", result.Requirements[1].Color);
			Assert.Equal("#ffa500", result.Requirements[2].Color);
			Assert.False(result.Requirements[3].HasColor);
		}

		[Fact]
		public void Parse_Color_RejectsInvalid()
		{
			DemandCompileResult result = DemandScriptParser.Parse(
				"req PMI -> MAN 06:00-08:00\n  color not-a-color\n");
			Assert.False(result.Success);
			Assert.Contains("color", string.Join(" ", result.Errors), StringComparison.OrdinalIgnoreCase);
		}

		[Fact]
		public void Planner_PropagatesScriptColorToCirculations()
		{
			string script = """
				req 1/h PMI -> MAN 06:00-08:00 as R-col
				  days lab
				  color #e11d48
				req 1/h PMI -> MAN 06:00-08:00 as R-def
				  days lab
				""";

			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			SfmDemoInfrastructure.Apply(topo);
			Plan plan = new Plan(topo);
			plan.EnsureDefaultTrainSpecs();
			Assert.True(plan.CompileDemand(script).Success, "compile");

			Mesh mesh = new MeshPlanner(plan).Solve(DayOfWeek.Monday);
			Assert.Contains(mesh.Circulations, c => c.DemandId == "R-col" && c.Color == "#e11d48");
			Assert.Contains(mesh.Circulations, c => c.DemandId == "R-def" && !c.HasColor);
		}

		[Fact]
		public void Planner_FiltersRequirementsByDayOfWeek()
		{
			string script = """
				require 1/h PMI -> MAN 06:00-10:00 as R-lab
				  days lab
				require 1/h PMI -> MAN 06:00-10:00 as R-fes
				  days fes
				""";

			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			SfmDemoInfrastructure.Apply(topo);
			Plan plan = new Plan(topo);
			plan.EnsureDefaultTrainSpecs();
			Assert.True(plan.CompileDemand(script).Success, "compile");

			Mesh mon = new MeshPlanner(plan).Solve(DayOfWeek.Monday);
			Mesh sun = new MeshPlanner(plan).Solve(DayOfWeek.Sunday);

			Assert.Equal(DayOfWeek.Monday, mon.PlanningDay);
			Assert.Equal(DayOfWeek.Sunday, sun.PlanningDay);
			// Solo el requisito del día correspondiente genera circulaciones
			Assert.All(mon.Circulations, c => Assert.Equal("R-lab", c.DemandId));
			Assert.All(sun.Circulations, c => Assert.Equal("R-fes", c.DemandId));
			Assert.All(mon.Errors, e => Assert.StartsWith("[lun]", e, StringComparison.Ordinal));
			Assert.All(mon.Warnings, w => Assert.StartsWith("[lun]", w, StringComparison.Ordinal));
			Assert.All(sun.Errors, e => Assert.StartsWith("[dom]", e, StringComparison.Ordinal));
			Assert.All(sun.Warnings, w => Assert.StartsWith("[dom]", w, StringComparison.Ordinal));
		}

		[Fact]
		public void Parse_DefinitionRegion_InheritsDaysAndColor()
		{
			string script = """
				days lab
				  color #e11d48
				    req PMI -> MAN 06:00-08:00 as R-nested
				      stops 30s
				    req PMI -> SPB 07:00-08:00 as R-same
				  req PMI -> INC 08:00-09:00 as R-days-only
				req PMI -> UIB 09:00-10:00 as R-outside
				""";

			DemandCompileResult result = DemandScriptParser.Parse(script);
			Assert.True(result.Success, string.Join("; ", result.Errors));
			Assert.Equal(4, result.Requirements.Count);

			DemandRequirement nested = result.Requirements[0];
			Assert.Equal("R-nested", nested.Id);
			Assert.Equal(ServiceDay.Laborables, nested.ServiceDays.Days);
			Assert.Equal("#e11d48", nested.Color);
			Assert.Equal(TimeSpan.FromSeconds(30), nested.Stops.DefaultDwell);

			Assert.Equal("#e11d48", result.Requirements[1].Color);
			Assert.Equal(ServiceDay.Laborables, result.Requirements[1].ServiceDays.Days);

			Assert.Equal(ServiceDay.Laborables, result.Requirements[2].ServiceDays.Days);
			Assert.False(result.Requirements[2].HasColor);

			Assert.Equal(ServiceDay.All, result.Requirements[3].ServiceDays.Days);
			Assert.False(result.Requirements[3].HasColor);
		}

		[Fact]
		public void Parse_WithKeyword_OpensRegion()
		{
			string script = """
				with days lab color #38bdf8
				  req PMI -> MAN 06:00-08:00 as R1
				con days fes
				  req PMI -> MAN 07:00-08:00 as R2
				region color orange
				  req PMI -> SPB 08:00-09:00 as R3
				""";

			DemandCompileResult result = DemandScriptParser.Parse(script);
			Assert.True(result.Success, string.Join("; ", result.Errors));
			Assert.Equal(3, result.Requirements.Count);
			Assert.Equal(ServiceDay.Laborables, result.Requirements[0].ServiceDays.Days);
			Assert.Equal("#38bdf8", result.Requirements[0].Color);
			Assert.Equal(ServiceDay.Festivos, result.Requirements[1].ServiceDays.Days);
			Assert.False(result.Requirements[1].HasColor);
			Assert.Equal("#ffa500", result.Requirements[2].Color);
			Assert.Equal(ServiceDay.All, result.Requirements[2].ServiceDays.Days);
		}

		[Fact]
		public void Parse_RequireOverride_BeatsRegion()
		{
			string script = """
				days lab color #111111
				  req PMI -> MAN 06:00-08:00 as R-keep
				  req PMI -> MAN 07:00-08:00 as R-override color #abcdef
				    days fes
				""";

			DemandCompileResult result = DemandScriptParser.Parse(script);
			Assert.True(result.Success, string.Join("; ", result.Errors));
			Assert.Equal("#111111", result.Requirements[0].Color);
			Assert.Equal(ServiceDay.Laborables, result.Requirements[0].ServiceDays.Days);
			Assert.Equal("#abcdef", result.Requirements[1].Color);
			Assert.Equal(ServiceDay.Festivos, result.Requirements[1].ServiceDays.Days);
		}

		[Fact]
		public void Planner_RegionDays_FiltersByDay()
		{
			string script = """
				days lab
				  req 1/h PMI -> MAN 06:00-10:00 as R-lab
				days fes
				  req 1/h PMI -> MAN 06:00-10:00 as R-fes
				""";

			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			SfmDemoInfrastructure.Apply(topo);
			Plan plan = new Plan(topo);
			plan.EnsureDefaultTrainSpecs();
			DemandCompileResult compiled = plan.CompileDemand(script);
			Assert.True(compiled.Success, string.Join("; ", compiled.Errors));

			Mesh mon = new MeshPlanner(plan).Solve(DayOfWeek.Monday);
			Mesh sun = new MeshPlanner(plan).Solve(DayOfWeek.Sunday);
			Assert.All(mon.Circulations, c => Assert.Equal("R-lab", c.DemandId));
			Assert.All(sun.Circulations, c => Assert.Equal("R-fes", c.DemandId));
		}

		[Fact]
		public void Parse_Delete_RangeAndAllFlag()
		{
			string script = """
				req PMI -> MAN 06:00-08:00 as R1
				delete 12:00-14:00
				delete 15:00-16:30 all
				del from 18:00 to 19:00
				""";

			DemandCompileResult result = DemandScriptParser.Parse(script);
			Assert.True(result.Success, string.Join("; ", result.Errors));
			Assert.Single(result.Requirements);
			Assert.Equal(3, result.Deletes.Count);

			Assert.Equal(0, result.Requirements[0].ScriptOrder);
			Assert.Equal(1, result.Deletes[0].ScriptOrder);
			Assert.False(result.Deletes[0].All);
			Assert.Equal(new TimeOnly(12, 0), result.Deletes[0].WindowStart);
			Assert.Equal(new TimeOnly(14, 0), result.Deletes[0].WindowEnd);

			Assert.True(result.Deletes[1].All);
			Assert.Equal(new TimeOnly(15, 0), result.Deletes[1].WindowStart);
			Assert.Equal(new TimeOnly(16, 30), result.Deletes[1].WindowEnd);

			Assert.False(result.Deletes[2].All);
			Assert.Equal(new TimeOnly(18, 0), result.Deletes[2].WindowStart);
		}

		[Fact]
		public void Parse_Delete_InheritsDaysFromRegion()
		{
			string script = """
				days lab
				  delete 12:00-14:00 all
				""";

			DemandCompileResult result = DemandScriptParser.Parse(script);
			Assert.True(result.Success, string.Join("; ", result.Errors));
			Assert.Single(result.Deletes);
			Assert.Equal(ServiceDay.Laborables, result.Deletes[0].ServiceDays.Days);
			Assert.True(result.Deletes[0].AppliesOn(DayOfWeek.Monday));
			Assert.False(result.Deletes[0].AppliesOn(DayOfWeek.Sunday));
		}

		[Fact]
		public void Planner_Delete_RemovesDeparturesInWindow_ThenAllowsSpecial()
		{
			// Base: un tren/h 06–10 → salidas 06,07,08,09.
			// delete 07:00-09:00 quita 07 y 08.
			// Especial a 07:30 se planifica después y permanece.
			string script = """
				days lab
				  req 1/h PMI -> MAN 06:00-10:00 as R-base
				  delete 07:00-09:00
				  req PMI -> MAN 07:30-08:30 as R-special
				    color #fbbf24
				""";

			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			SfmDemoInfrastructure.Apply(topo);
			Plan plan = new Plan(topo);
			plan.EnsureDefaultTrainSpecs();
			DemandCompileResult compiled = plan.CompileDemand(script);
			Assert.True(compiled.Success, string.Join("; ", compiled.Errors));

			Mesh mesh = new MeshPlanner(plan).Solve(DayOfWeek.Monday);
			Assert.Contains(mesh.Circulations, c => c.DemandId == "R-special");
			Assert.DoesNotContain(
				mesh.Circulations,
				c => c.DemandId == "R-base"
					&& c.Departure >= new TimeSpan(7, 0, 0)
					&& c.Departure < new TimeSpan(9, 0, 0));
			Assert.Contains(
				mesh.Circulations,
				c => c.DemandId == "R-base" && c.Departure == new TimeSpan(6, 0, 0));
			Assert.Contains(
				mesh.Circulations,
				c => c.DemandId == "R-base" && c.Departure == new TimeSpan(9, 0, 0));
			Assert.All(
				mesh.Circulations.Where(c => c.DemandId == "R-special"),
				c => Assert.Equal("#fbbf24", c.Color));
		}

		[Fact]
		public void Planner_DeleteAll_RemovesJourneyOverlapNotOnlyDeparture()
		{
			// Tren sale 06:00 y llega ~06:48. Franja 06:30-07:00:
			// - sin all: no borra (salida 06:00 fuera de franja)
			// - con all: borra (trayecto solapa 06:30-06:48)
			string withDepDelete = """
				days lab
				  req PMI -> MAN 06:00-07:00 as R-one
				  delete 06:30-07:00
				""";

			string withAllDelete = """
				days lab
				  req PMI -> MAN 06:00-07:00 as R-one
				  delete 06:30-07:00 all
				""";

			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			SfmDemoInfrastructure.Apply(topo);

			Plan planDep = new Plan(topo);
			planDep.EnsureDefaultTrainSpecs();
			Assert.True(planDep.CompileDemand(withDepDelete).Success);
			Mesh meshDep = new MeshPlanner(planDep).Solve(DayOfWeek.Monday);
			Assert.Single(meshDep.Circulations);
			Assert.True(
				meshDep.Circulations[0].Arrival > new TimeSpan(6, 30, 0),
				"el trayecto debe solapar 06:30 para que all tenga efecto");

			Plan planAll = new Plan(topo);
			planAll.EnsureDefaultTrainSpecs();
			Assert.True(planAll.CompileDemand(withAllDelete).Success);
			Mesh meshAll = new MeshPlanner(planAll).Solve(DayOfWeek.Monday);
			Assert.Empty(meshAll.Circulations);
		}
	}
}
