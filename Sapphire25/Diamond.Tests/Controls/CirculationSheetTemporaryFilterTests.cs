using Diamond.Controls.Rendering;
using Diamond.Timed;
using Diamond.Topo;

namespace Diamond.Tests.Controls
{
	public class CirculationSheetTemporaryFilterTests
	{
		[Fact]
		public void CompleteSheet_FiltersTempsBySenseAndBabBau()
		{
			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			SfmDemoInfrastructure.Apply(topo);
			List<TemporarySpeedLimit> temps = new List<TemporarySpeedLimit>
			{
				TopoTemporaryLimits.FromSpan(
					"T3", 5000L, 6000L, 40, TemporaryLimitReason.Works, "BAB v1",
					TemporaryLimitTrack.Track1),
				TopoTemporaryLimits.FromSpan(
					"T3", 7000L, 8000L, 35, TemporaryLimitReason.Geometry, "BAB v2",
					TemporaryLimitTrack.Track2),
				TopoTemporaryLimits.FromSpan(
					"T3", 10000L, 11000L, 45, TemporaryLimitReason.Works, "BAB ambas",
					TemporaryLimitTrack.Both),
				TopoTemporaryLimits.FromSpan(
					"T3", 40000L, 41000L, 30, TemporaryLimitReason.Works, "BAU v1",
					TemporaryLimitTrack.Track1),
				TopoTemporaryLimits.FromSpan(
					"T3", 42000L, 43000L, 25, TemporaryLimitReason.Geometry, "BAU v2",
					TemporaryLimitTrack.Track2)
			};
			TopoTemporaryLimits.Apply(topo, temps);

			Plan plan = new Plan(topo);
			plan.EnsureDefaultTrainSpecs();
			Assert.True(plan.CompileDemand("""
				require both ways every 60 min PMI -> MAN 06:00-10:00 as R-T3
				  days lab
				  stops 30s
				""").Success);
			Mesh mesh = new MeshPlanner(plan).Solve(DayOfWeek.Monday);

			Circulation? up = null;
			Circulation? down = null;
			int i = 0;
			while (i < mesh.Circulations.Count)
			{
				Circulation c = mesh.Circulations[i];
				i++;
				if (string.Equals(c.Asimilation.Origin.Station.Avr, "PMI", StringComparison.OrdinalIgnoreCase)
					&& string.Equals(c.Asimilation.Destination.Station.Avr, "MAN", StringComparison.OrdinalIgnoreCase))
				{
					up = c;
				}

				if (string.Equals(c.Asimilation.Origin.Station.Avr, "MAN", StringComparison.OrdinalIgnoreCase)
					&& string.Equals(c.Asimilation.Destination.Station.Avr, "PMI", StringComparison.OrdinalIgnoreCase))
				{
					down = c;
				}
			}

			Assert.NotNull(up);
			Assert.NotNull(down);
			Assert.True(TrainNumbering.IsNetworkAscendingForNumbering(up!));
			Assert.False(TrainNumbering.IsNetworkAscendingForNumbering(down!));

			CirculationSheetDocument upDoc = CirculationSheetDocument.Build(
				up!, mesh, 36, includeTemporaryLimits: true);
			CirculationSheetDocument downDoc = CirculationSheetDocument.Build(
				down!, mesh, 36, includeTemporaryLimits: true);

			Assert.True(HasTemporaryAt(upDoc, up!.Asimilation.View, 5500L));
			Assert.False(HasTemporaryAt(upDoc, up.Asimilation.View, 7500L));
			Assert.True(HasTemporaryAt(upDoc, up.Asimilation.View, 10500L));
			Assert.True(HasTemporaryAt(upDoc, up.Asimilation.View, 40500L));
			Assert.False(HasTemporaryAt(upDoc, up.Asimilation.View, 42500L));
			Assert.Equal(40, VmaxAt(upDoc, up.Asimilation.View, 5500L));
			Assert.Equal(35, VmaxAt(downDoc, down!.Asimilation.View, 7500L));

			Assert.False(HasTemporaryAt(downDoc, down.Asimilation.View, 5500L));
			Assert.True(HasTemporaryAt(downDoc, down.Asimilation.View, 7500L));
			Assert.True(HasTemporaryAt(downDoc, down.Asimilation.View, 10500L));
			Assert.True(HasTemporaryAt(downDoc, down.Asimilation.View, 40500L));
			Assert.False(HasTemporaryAt(downDoc, down.Asimilation.View, 42500L));
		}

		private static bool HasTemporaryAt(CirculationSheetDocument doc, RouteView view, long axisPk)
		{
			CirculationSheetFrontier? row = FrontierCovering(doc, view, axisPk);
			return row is not null && row.OutgoingIsTemporary;
		}

		private static int? VmaxAt(CirculationSheetDocument doc, RouteView view, long axisPk)
		{
			CirculationSheetFrontier? row = FrontierCovering(doc, view, axisPk);
			return row?.OutgoingVmaxKmh;
		}

		private static CirculationSheetFrontier? FrontierCovering(
			CirculationSheetDocument doc,
			RouteView view,
			long axisPk)
		{
			if (!view.TryMapAxisToRoute(view.Legs[0].Axis, axisPk, out long routePk))
			{
				return null;
			}

			int i = 0;
			while (i < doc.Frontiers.Count - 1)
			{
				CirculationSheetFrontier row = doc.Frontiers[i];
				CirculationSheetFrontier next = doc.Frontiers[i + 1];
				i++;
				long a = row.RoutePk;
				long b = next.RoutePk;
				long lo = a < b ? a : b;
				long hi = a > b ? a : b;
				if (routePk >= lo && routePk < hi)
				{
					return row;
				}
			}

			return null;
		}
	}
}
