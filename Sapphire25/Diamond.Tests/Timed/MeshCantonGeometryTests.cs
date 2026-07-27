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

		private static Plan CreatePlanWithCorridor()
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
