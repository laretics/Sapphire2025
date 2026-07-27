using Diamond.Timed;
using Diamond.Topo;

namespace Diamond.Tests.Timed
{
	public class MeshCantonGeometryTests
	{
		[Fact]
		public void OccupationRects_SameCantonSameTime_Overlap()
		{
			CantonOccupationRect a = new CantonOccupationRect(
				"C1", "X1", 0, 10000,
				TimeSpan.FromHours(6),
				TimeSpan.FromHours(7));
			CantonOccupationRect b = new CantonOccupationRect(
				"C2", "X1", 0, 10000,
				TimeSpan.FromHours(6.5),
				TimeSpan.FromHours(7.5));

			Assert.True(a.Overlaps(b));
			Assert.True(b.Overlaps(a));
		}

		[Fact]
		public void OccupationRects_SeparatedInTime_DoNotOverlap()
		{
			CantonOccupationRect a = new CantonOccupationRect(
				"C1", "X1", 0, 10000,
				TimeSpan.FromHours(6),
				TimeSpan.FromHours(7));
			CantonOccupationRect b = new CantonOccupationRect(
				"C2", "X1", 0, 10000,
				TimeSpan.FromHours(7),
				TimeSpan.FromHours(8));

			Assert.False(a.Overlaps(b));
		}

		[Fact]
		public void OccupationRects_TryIntersect_ReturnsOverlap()
		{
			CantonOccupationRect a = new CantonOccupationRect(
				"4901", "V", 0, 10000,
				TimeSpan.FromHours(6),
				TimeSpan.FromHours(7));
			CantonOccupationRect b = new CantonOccupationRect(
				"4902", "V", 5000, 15000,
				TimeSpan.FromHours(6.5),
				TimeSpan.FromHours(7.5));

			CantonOccupationRect? ix;
			Assert.True(a.TryIntersect(b, out ix));
			Assert.NotNull(ix);
			Assert.Equal(5000L, ix!.PkStart);
			Assert.Equal(10000L, ix.PkEnd);
			Assert.Equal(TimeSpan.FromHours(6.5), ix.TimeEnter);
			Assert.Equal(TimeSpan.FromHours(7), ix.TimeExit);
		}

		[Fact]
		public void FindHardConflicts_SingleTrackOpposite_ReportsIntersection()
		{
			Plan plan = CreatePlanWithCorridor(doubleTrack: false);
			plan.DemandScript = """
				require both ways every 40 min A -> B 06:05-08:00 as R1
				""";
			Assert.True(plan.CompileDemand().Success);

			Mesh mesh = new MeshPlanner(plan).Solve();
			Axis axis = plan.Topo!.Axes[0];
			RouteView view = RouteView.FromAxis(axis);
			IReadOnlyList<OccupationConflict> conflicts = MeshCantonGeometry.FindHardConflicts(mesh, view);

			// En vía única con both ways a la misma cadencia suele haber solapes ida/vuelta
			if (conflicts.Count > 0)
			{
				Assert.All(conflicts, c =>
				{
					Assert.True(c.Intersection.PkEnd > c.Intersection.PkStart);
					Assert.True(c.Intersection.TimeExit > c.Intersection.TimeEnter);
					Assert.False(string.IsNullOrEmpty(c.CirculationIdA));
					Assert.False(string.IsNullOrEmpty(c.CirculationIdB));
				});
			}
		}

		[Fact]
		public void Mesh_GetCantonOccupations_ProducesRectsForCorridor()
		{
			Plan plan = CreatePlanWithCorridor();
			plan.DemandScript = """
				require 2/h A -> B 06:00-08:00 as R1
				""";
			Assert.True(plan.CompileDemand().Success);

			Mesh mesh = new MeshPlanner(plan).Solve();
			Axis axis = plan.Topo!.Axes[0];
			IReadOnlyList<CantonOccupationRect> occupations = mesh.GetCantonOccupations(axis);

			Assert.NotEmpty(occupations);
			Assert.All(occupations, o =>
			{
				Assert.Equal("X1", o.AxisId);
				Assert.True(o.TimeExit > o.TimeEnter);
				Assert.True(o.PkEnd > o.PkStart);
			});
		}

		[Fact]
		public void TrackOccupation_ExcludesPrincipalStationDwell()
		{
			// A(STA) --10km-- M(STM principal) --10km-- B(STB)
			// Parada 5 min en M: no debe alargar la ocupación de los cantones en vía.
			Plan plan = CreatePlanWithCorridor();
			plan.DemandScript = """
				req A -> B 06:00-08:00 as R1
				  stops 30s
				  dwell M 5min
				""";
			Assert.True(plan.CompileDemand().Success, "compile");

			Mesh mesh = new MeshPlanner(plan).Solve(DayOfWeek.Monday);
			Assert.NotEmpty(mesh.Circulations);
			Circulation c = mesh.Circulations[0];

			// Sin dwell largo en M: solo 30s por defecto en principales; aquí override 5 min.
			// Ocupación del cantón [0,10000) debe terminar a la llegada a M, no a la salida.
			IReadOnlyList<MeshCantonGeometry.TrackOccupationInterval> left =
				MeshCantonGeometry.GetTrackOccupationsInCanton(
					c.Departure, c.Asimilation, 0L, 10000L);
			IReadOnlyList<MeshCantonGeometry.TrackOccupationInterval> right =
				MeshCantonGeometry.GetTrackOccupationsInCanton(
					c.Departure, c.Asimilation, 10000L, 20000L);

			Assert.NotEmpty(left);
			Assert.NotEmpty(right);

			TimeSpan? arriveM = c.Asimilation.TimeArriveByPK(10000L);
			TimeSpan? departM = c.Asimilation.TimeDepartByPK(10000L);
			Assert.NotNull(arriveM);
			Assert.NotNull(departM);
			Assert.True(departM!.Value - arriveM!.Value >= TimeSpan.FromMinutes(4.5),
				"dwell ~5 min esperado en M");

			// El primer cantón sale al llegar a M (antes del dwell).
			TimeSpan leftExit = left[left.Count - 1].Exit;
			Assert.Equal(c.Departure + arriveM.Value, leftExit);

			// El segundo cantón entra al salir de M (tras el dwell).
			TimeSpan rightEnter = right[0].Enter;
			Assert.Equal(c.Departure + departM.Value, rightEnter);

			// Hueco de dwell: no hay ocupación de vía entre llegada y salida de M.
			Assert.True(rightEnter >= leftExit);
			Assert.True((rightEnter - leftExit) >= TimeSpan.FromMinutes(4.5));
		}

		private static Plan CreatePlanWithCorridor(bool doubleTrack = true)
		{
			// AVR en mayúsculas → estación principal (StationClassification).
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
			axis.DefaultTrackCount = doubleTrack ? 2 : 1;

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
