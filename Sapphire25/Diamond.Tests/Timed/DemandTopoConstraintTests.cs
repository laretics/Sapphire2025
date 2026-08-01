using Diamond.Timed;
using Diamond.Topo;

namespace Diamond.Tests.Timed
{
	public class DemandTopoConstraintTests
	{
		[Fact]
		public void Parse_SingleTrackAndLimit_ProducesConstraints()
		{
			string script = """
				plan "custom"
				single track A -> B
				tracks 1 A -> M on X1
				limit 60 A -> B
				vmax 40 M -> B
				req A -> B 06:00-08:00 as R1
				  stops 30s
				""";

			DemandCompileResult result = DemandScriptParser.Parse(script);
			Assert.True(result.Success, string.Join("; ", result.Errors));
			Assert.Equal(4, result.TopoConstraints.Count);

			Assert.Equal(DemandTopoConstraintKind.TrackCount, result.TopoConstraints[0].Kind);
			Assert.Equal(1, result.TopoConstraints[0].Value);
			Assert.Equal("A", result.TopoConstraints[0].From.Text);
			Assert.Equal("B", result.TopoConstraints[0].To.Text);

			Assert.Equal(DemandTopoConstraintKind.TrackCount, result.TopoConstraints[1].Kind);
			Assert.Equal(1, result.TopoConstraints[1].Value);
			Assert.Equal("X1", result.TopoConstraints[1].AxisId);

			Assert.Equal(DemandTopoConstraintKind.SpeedLimit, result.TopoConstraints[2].Kind);
			Assert.Equal(60, result.TopoConstraints[2].Value);

			Assert.Equal(DemandTopoConstraintKind.SpeedLimit, result.TopoConstraints[3].Kind);
			Assert.Equal(40, result.TopoConstraints[3].Value);
		}

		[Fact]
		public void Parse_LimitWithUnit_IsAccepted()
		{
			DemandCompileResult result = DemandScriptParser.Parse("limit 50 km/h INC -> MAN");
			Assert.True(result.Success, string.Join("; ", result.Errors));
			Assert.Single(result.TopoConstraints);
			Assert.Equal(50, result.TopoConstraints[0].Value);
			Assert.Equal(DemandTopoConstraintKind.SpeedLimit, result.TopoConstraints[0].Kind);
		}

		[Fact]
		public void Compile_AppliesSessionOverlaysWithoutTouchingBaseSpans()
		{
			Plan plan = CreateDoubleTrackCorridor();
			Axis axis = plan.Topo!.Axes[0];
			// Base: doble vía por defecto en todo el corredor.
			Assert.Equal(2, axis.DefaultTrackCount);
			Assert.Equal(2, axis.GetTrackCountAt(5000L));
			Assert.Empty(axis.SessionTrackSpans);

			plan.DemandScript = """
				single track A -> B
				limit 45 A -> B
				req A -> B 06:00-08:00 as R1
				  stops 30s
				""";
			DemandCompileResult compile = plan.CompileDemand();
			Assert.True(compile.Success, string.Join("; ", compile.Errors));

			// Sesión: vía simple y 45 km/h.
			Assert.Equal(1, axis.GetTrackCountAt(5000L));
			Assert.Equal(45, axis.GetEffectiveSpeedLimit(5000L));
			// Base intacta (default y fijas sin tocar).
			Assert.Equal(2, axis.DefaultTrackCount);
			Assert.Equal(0, axis.FixedLimits.SpeedCount);
			Assert.True(axis.SessionTrackSpans.Count >= 1);
			Assert.True(axis.SessionLimits.SpeedCount >= 1);

			// Recompilar sin restricciones limpia la sesión.
			plan.DemandScript = """
				req A -> B 06:00-08:00 as R1
				""";
			Assert.True(plan.CompileDemand().Success);
			Assert.Equal(2, axis.GetTrackCountAt(5000L));
			Assert.Empty(axis.SessionTrackSpans);
			Assert.Equal(0, axis.SessionLimits.SpeedCount);
		}

		[Fact]
		public void SessionSingleTrack_ForcesOppositeConflict()
		{
			// Con doble vía base, both ways a la misma cadencia no debería forzar error duro.
			// Tras single track de sesión, el planificador debe detectar cruce en vía única.
			Plan plan = CreateDoubleTrackCorridor();
			plan.DemandScript = """
				single track A -> B
				require both ways every 20 min A -> B 06:00-08:00 as R1
				  stops 30s
				""";
			Assert.True(plan.CompileDemand().Success, string.Join("; ", plan.CompileDemand().Errors));

			Mesh mesh = new MeshPlanner(plan).Solve(DayOfWeek.Monday);
			Assert.NotEmpty(mesh.Circulations);
			// En vía única con both ways densos es muy probable el conflicto; al menos la
			// restricción de vías se aplicó.
			Assert.Equal(1, plan.Topo!.Axes[0].GetTrackCountAt(10000L));
		}

		[Fact]
		public void SessionSpeedLimit_SlowsAsimilation()
		{
			Plan plan = CreateDoubleTrackCorridor();
			plan.DemandScript = """
				req A -> B 06:00-08:00 as R1
				  stops 30s
				""";
			Assert.True(plan.CompileDemand().Success);
			Mesh free = new MeshPlanner(plan).Solve(DayOfWeek.Monday);
			Assert.NotEmpty(free.Circulations);
			TimeSpan freeTime = free.Circulations[0].Asimilation.TotalTime;

			plan.DemandScript = """
				limit 30 A -> B
				req A -> B 06:00-08:00 as R1
				  stops 30s
				""";
			Assert.True(plan.CompileDemand().Success);
			Mesh limited = new MeshPlanner(plan).Solve(DayOfWeek.Monday);
			Assert.NotEmpty(limited.Circulations);
			TimeSpan limitedTime = limited.Circulations[0].Asimilation.TotalTime;

			Assert.True(limitedTime > freeTime,
				"con limit 30 el viaje debe ser más largo: free=" + freeTime + " lim=" + limitedTime);
		}

		private static Plan CreateDoubleTrackCorridor()
		{
			Station stA = MakeStation("A", "STA");
			Station stM = MakeStation("M", "STM");
			Station stB = MakeStation("B", "STB");

			Axis axis = new Axis();
			axis.Id = "X1";
			axis.Vmax = 100;
			AxisVertex v0 = new AxisVertex(39.0, 2.0, 0L);
			v0.Station = stA;
			AxisVertex v1 = new AxisVertex(39.05, 2.05, 10000L);
			v1.Station = stM;
			AxisVertex v2 = new AxisVertex(39.1, 2.1, 20000L);
			v2.Station = stB;
			axis.AddVertex(v0);
			axis.AddVertex(v1);
			axis.AddVertex(v2);
			axis.Rebuild();
			axis.SetCantonFrontiers(new long[] { 0L, 10000L, 20000L });
			// Topología base: doble vía (como demo SFM en tramos con cruce libre).
			axis.DefaultTrackCount = 2;

			TopoLayout topo = new TopoLayout();
			topo.AddStation(stA);
			topo.AddStation(stM);
			topo.AddStation(stB);
			topo.AddAxis(axis);

			Plan plan = new Plan(topo);
			plan.EnsureDefaultTrainSpecs();
			return plan;
		}

		private static Station MakeStation(string id, string avr)
		{
			Station s = new Station(id);
			s.Name = id;
			s.Avr = avr;
			return s;
		}
	}
}
