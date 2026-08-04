using Diamond.Motion;
using Diamond.Timed;

namespace Diamond.Tests.Timed
{
	public class TrainSpecsScriptTests
	{
		[Fact]
		public void Parse_Train_SingleLine_AllProperties()
		{
			DemandCompileResult result = DemandScriptParser.Parse("""
				train s3300 "Civia S-3300" accel 0.85 brake 0.75 vmax 120
				""");

			Assert.True(result.Success, string.Join("; ", result.Errors));
			Assert.Single(result.Fleet);
			TrainSpecs t = result.Fleet[0];
			Assert.Equal("s3300", t.Id);
			Assert.Equal("Civia S-3300", t.Name);
			Assert.Equal(0.85, t.Acceleration, 5);
			Assert.Equal(0.75, t.ServiceBrake, 5);
			Assert.Equal(120.0, t.MaxSpeedKmh, 5);
		}

		[Fact]
		public void Parse_Train_Multiline_AndTrenAlias()
		{
			DemandCompileResult result = DemandScriptParser.Parse("""
				tren metro
				  name "UT Metro"
				  accel 1.0 m/s2
				  brake 0.9
				  vmax 100 km/h
				""");

			Assert.True(result.Success, string.Join("; ", result.Errors));
			Assert.Single(result.Fleet);
			Assert.Equal("metro", result.Fleet[0].Id);
			Assert.Equal("UT Metro", result.Fleet[0].Name);
			Assert.Equal(1.0, result.Fleet[0].Acceleration, 5);
			Assert.Equal(0.9, result.Fleet[0].ServiceBrake, 5);
			Assert.Equal(100.0, result.Fleet[0].MaxSpeedKmh, 5);
		}

		[Fact]
		public void Parse_Train_OmittedProps_UseDefaultModelValues()
		{
			TrainSpecs defaults = TrainSpecs.DefaultModel;
			DemandCompileResult result = DemandScriptParser.Parse("train lite");
			Assert.True(result.Success, string.Join("; ", result.Errors));
			Assert.Single(result.Fleet);
			Assert.Equal("lite", result.Fleet[0].Id);
			Assert.Equal("lite", result.Fleet[0].Name);
			Assert.Equal(defaults.Acceleration, result.Fleet[0].Acceleration, 5);
			Assert.Equal(defaults.ServiceBrake, result.Fleet[0].ServiceBrake, 5);
			Assert.Equal(defaults.MaxSpeedKmh, result.Fleet[0].MaxSpeedKmh, 5);
		}

		[Fact]
		public void Parse_Train_DuplicateId_Errors()
		{
			DemandCompileResult result = DemandScriptParser.Parse("""
				train series-a "Uno" vmax 100
				train series-a "Dos" vmax 120
				""");

			Assert.False(result.Success);
			Assert.Contains("duplicado", string.Join(" ", result.Errors), StringComparison.OrdinalIgnoreCase);
		}

		[Fact]
		public void Plan_CompileDemand_WithoutTrain_EnsuresDefault()
		{
			Plan plan = new Plan();
			DemandCompileResult result = plan.CompileDemand("""
				plan "solo default"
				req PMI -> MAN 06:00-07:00 as R1
				""");

			Assert.True(result.Success, string.Join("; ", result.Errors));
			Assert.Empty(result.Fleet);
			Assert.NotNull(plan.FindTrainSpecsById("default"));
			Assert.Equal(TrainSpecs.DefaultModel.Acceleration, plan.FindTrainSpecsById("default")!.Acceleration, 5);
		}

		[Fact]
		public void Plan_CompileDemand_WithTrain_ReplacesFleet()
		{
			Plan plan = new Plan();
			plan.EnsureDefaultTrainSpecs();
			plan.AddTrainSpecs(new TrainSpecs("old", "Viejo", 0.5, 0.5, 80.0));

			DemandCompileResult result = plan.CompileDemand("""
				train express "Rápido" accel 1.1 brake 1.0 vmax 140
				train local "Cercanías" vmax 100
				req PMI -> MAN 06:00-07:00 using express as R1
				""");

			Assert.True(result.Success, string.Join("; ", result.Errors));
			Assert.Equal(2, plan.Fleet.Count);
			Assert.Null(plan.FindTrainSpecsById("default"));
			Assert.Null(plan.FindTrainSpecsById("old"));
			Assert.NotNull(plan.FindTrainSpecsById("express"));
			Assert.Equal(1.1, plan.FindTrainSpecsById("express")!.Acceleration, 5);
			Assert.Equal(100.0, plan.FindTrainSpecsById("local")!.MaxSpeedKmh, 5);
		}

		[Fact]
		public void Plan_HeaderStyle_IncludePlanTrainRequire()
		{
			Plan plan = new Plan();
			plan.ScriptBaseDirectory = Path.GetDirectoryName(SamplePaths.TopoSfm227) ?? string.Empty;

			DemandCompileResult result = plan.CompileDemand("""
				include toposfm227
				plan "cabecera completa"
				train default "SFM" accel 0.9 brake 0.8 vmax 100
				require both ways every 60 min PMI -> MAN 06:00-08:00 using default as R-T3
				  days lab
				""");

			Assert.True(result.Success, string.Join("; ", result.Errors));
			Assert.NotNull(plan.Topo);
			Assert.Equal("cabecera completa", plan.Name);
			Assert.Single(plan.Fleet);
			Assert.Equal(100.0, plan.Fleet[0].MaxSpeedKmh, 5);
			Assert.True(plan.Demand[0].IsResolved);
			Assert.Equal("default", plan.Demand[0].FleetId);
		}
	}
}
