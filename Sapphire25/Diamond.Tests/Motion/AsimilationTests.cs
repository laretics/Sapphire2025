using Diamond.Motion;
using Diamond.Topo;

namespace Diamond.Tests.Motion
{
	public class AsimilationTests
	{
		[Fact]
		public void DefaultModel_HasExpectedKinematics()
		{
			TrainSpecs specs = TrainSpecs.DefaultModel;
			Assert.Equal(0.9, specs.Acceleration, 5);
			Assert.Equal(0.8, specs.ServiceBrake, 5);
			Assert.Equal(160.0, specs.MaxSpeedKmh, 5);
		}

		[Fact]
		public void IncreasingSense_FromOriginToDestination()
		{
			Axis axis = CreateStraightAxis(pkStart: 0L, pkEnd: 5000L, vmax: 100);
			StationOnAxis origin = Place("A", 0L);
			StationOnAxis dest = Place("B", 5000L);

			Asimilation asim = new Asimilation(
				axis,
				TrainSpecs.DefaultModel,
				origin,
				dest,
				intermediateStops: null);

			Assert.Equal(CirculationSense.IncreasingPk, asim.Sense);
			Assert.Same(origin, asim.Origin);
			Assert.Same(dest, asim.Destination);
			Assert.Equal(0.0, asim.SpeedByPK(0L), 3);
			Assert.Equal(0.0, asim.SpeedByPK(5000L), 3);
			Assert.True(asim.SpeedByPK(2500L) > 10.0);
			Assert.True(asim.SpeedByPK(2500L) <= 100.0 + 0.1);
			Assert.Equal(0L, asim.PKByTime(TimeSpan.Zero));
			Assert.Equal(5000L, asim.PKByTime(asim.TotalTime));
		}

		[Fact]
		public void DecreasingSense_RunsTowardLowerPk()
		{
			Axis axis = CreateStraightAxis(0L, 5000L, 100);
			StationOnAxis origin = Place("B", 5000L);
			StationOnAxis dest = Place("A", 0L);

			Asimilation asim = new Asimilation(
				axis,
				TrainSpecs.DefaultModel,
				origin,
				dest);

			Assert.Equal(CirculationSense.DecreasingPk, asim.Sense);
			Assert.Equal(0.0, asim.SpeedByPK(5000L), 3);
			Assert.Equal(0.0, asim.SpeedByPK(0L), 3);
			Assert.True(asim.SpeedByPK(2500L) > 10.0);

			Assert.Equal(5000L, asim.PKByTime(TimeSpan.Zero));
			Assert.Equal(0L, asim.PKByTime(asim.TotalTime));

			// A mitad de tiempo debe estar entre destino y origen (PK bajando).
			long mid = asim.PKByTime(TimeSpan.FromSeconds(asim.TotalTime.TotalSeconds * 0.5));
			Assert.InRange(mid, 0L, 5000L);
		}

		[Fact]
		public void IntermediateStop_ForcesZeroSpeed_AndDwell()
		{
			Axis axis = CreateStraightAxis(0L, 10000L, 120);
			axis.FixedLimits.Add(60, 0L, 10000L);

			StationOnAxis origin = Place("A", 0L);
			StationOnAxis mid = Place("M", 4000L);
			StationOnAxis dest = Place("B", 10000L);

			Asimilation asim = new Asimilation(
				axis,
				TrainSpecs.DefaultModel,
				origin,
				dest,
				new[]
				{
					new AsimilationStop(mid, TimeSpan.FromSeconds(20))
				});

			Assert.Equal(0.0, asim.SpeedByPK(4000L), 3);
			Assert.True(asim.SpeedByPK(2000L) > 0.0);
			Assert.True(asim.SpeedByPK(7000L) > 0.0);
			Assert.True(asim.SpeedByPK(2000L) <= 60.0 + 0.5);
			Assert.True(asim.TotalTime > TimeSpan.FromSeconds(20));
		}

		[Fact]
		public void IntermediateStop_OutsidePath_Throws()
		{
			Axis axis = CreateStraightAxis(0L, 5000L, 80);
			StationOnAxis origin = Place("A", 1000L);
			StationOnAxis dest = Place("B", 4000L);
			StationOnAxis outside = Place("X", 4500L);

			Assert.Throws<ArgumentException>(() =>
				new Asimilation(
					axis,
					TrainSpecs.DefaultModel,
					origin,
					dest,
					new[] { new AsimilationStop(outside, TimeSpan.Zero) }));
		}

		[Fact]
		public void SameOriginAndDestinationPk_Throws()
		{
			Axis axis = CreateStraightAxis(0L, 1000L, 50);
			StationOnAxis a = Place("A", 100L);
			Assert.Throws<ArgumentException>(() =>
				new Asimilation(axis, TrainSpecs.DefaultModel, a, a));
		}

		[Fact]
		public void TemporaryLimits_DoNotChangeMarchTime()
		{
			Axis axis = CreateStraightAxis(0L, 10000L, 100);
			StationOnAxis origin = Place("A", 0L);
			StationOnAxis dest = Place("B", 10000L);
			Asimilation withoutTemp = new Asimilation(axis, TrainSpecs.DefaultModel, origin, dest);
			TimeSpan tasado = withoutTemp.TotalTime;

			TopoLayout layout = new TopoLayout();
			layout.AddAxis(axis);
			TopoTemporaryLimits.Apply(
				layout,
				new[] { TopoTemporaryLimits.FromSpan("TEST", 2000L, 8000L, 20) });
			Assert.Equal(20, axis.GetEffectiveSpeedLimit(4000L));
			Assert.Equal(100, RouteView.FromAxis(axis).GetScheduledSpeedLimit(4000L));

			Asimilation withTemp = new Asimilation(axis, TrainSpecs.DefaultModel, origin, dest);
			Assert.Equal(tasado.TotalSeconds, withTemp.TotalTime.TotalSeconds, 3);
		}

		private static Axis CreateStraightAxis(long pkStart, long pkEnd, int vmax)
		{
			Axis axis = new Axis();
			axis.Id = "TEST";
			axis.Vmax = vmax;
			axis.AddVertex(new AxisVertex(39.0, 2.0, pkStart));
			axis.AddVertex(new AxisVertex(39.1, 2.1, pkEnd));
			axis.Rebuild();
			return axis;
		}

		private static StationOnAxis Place(string id, long pk)
		{
			Station station = new Station(id);
			station.Name = id;
			return new StationOnAxis(station, pk);
		}
	}
}
