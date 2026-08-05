using Diamond.Timed;

namespace Diamond.Tests.Timed
{
	public class PlanNotesScriptTests
	{
		[Fact]
		public void Parse_PlanAndNotes_Captured()
		{
			DemandCompileResult result = DemandScriptParser.Parse("""
				plan "SFM T3 laborables"
				notes "Malla de pruebas del programa Diamond"
				req PMI -> MAN 06:00-07:00 as R1
				""");

			Assert.True(result.Success, string.Join("; ", result.Errors));
			Assert.Equal("SFM T3 laborables", result.PlanName);
			Assert.Equal("Malla de pruebas del programa Diamond", result.Notes);
		}

		[Fact]
		public void CompileDemand_AppliesPlanNameAndNotesToPlan()
		{
			Plan plan = new Plan();
			plan.Name = "nombre-previo-del-host";
			DemandCompileResult result = plan.CompileDemand("""
				plan "SFM T3 laborables"
				notes "Malla de pruebas del programa Diamond"
				req PMI -> MAN 06:00-07:00 as R1
				""");

			Assert.True(result.Success, string.Join("; ", result.Errors));
			Assert.Equal("SFM T3 laborables", plan.Name);
			Assert.Equal("Malla de pruebas del programa Diamond", plan.Notes);
		}

		[Fact]
		public void CompileDemand_WithoutNotes_ClearsPlanNotes()
		{
			Plan plan = new Plan();
			plan.Notes = "vieja";
			DemandCompileResult result = plan.CompileDemand("""
				plan "Solo plan"
				req PMI -> MAN 06:00-07:00 as R1
				""");

			Assert.True(result.Success, string.Join("; ", result.Errors));
			Assert.Equal("Solo plan", plan.Name);
			Assert.Equal(string.Empty, plan.Notes);
		}
	}
}
