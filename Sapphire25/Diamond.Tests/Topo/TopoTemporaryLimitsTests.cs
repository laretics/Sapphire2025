using Diamond.Topo;

namespace Diamond.Tests.Topo
{
	public class TopoTemporaryLimitsTests
	{
		[Fact]
		public void Apply_AddsLayersOnMatchingAxes_AndClearsPrevious()
		{
			TopoLayout layout = new TopoLayout();
			Axis t3 = new Axis();
			t3.Id = "T3";
			t3.Vmax = 100;
			Axis m1 = new Axis();
			m1.Id = "M1";
			m1.Vmax = 80;
			layout.AddAxis(t3);
			layout.AddAxis(m1);
			t3.TemporaryLimits.Add(20, 0L, 100L);

			List<TemporarySpeedLimit> limits = new List<TemporarySpeedLimit>
			{
				TopoTemporaryLimits.FromSpan("T3", 1000L, 4000L, 80),
				TopoTemporaryLimits.FromSpan("T3", 2000L, 2500L, 30),
				TopoTemporaryLimits.FromSpan("M1", 100L, 200L, 40),
				TopoTemporaryLimits.FromSpan("NOPE", 0L, 10L, 10)
			};

			TopoTemporaryLimits.Apply(layout, limits);

			Assert.Null(t3.GetTemporarySpeedLimit(50L));
			Assert.Equal(80, t3.GetTemporarySpeedLimit(1500L));
			Assert.Equal(30, t3.GetTemporarySpeedLimit(2200L));
			Assert.Equal(40, m1.GetTemporarySpeedLimit(150L));
			Assert.Equal(30, t3.GetEffectiveSpeedLimit(2200L));
		}

		[Fact]
		public void Apply_Empty_ClearsAllAxes()
		{
			TopoLayout layout = new TopoLayout();
			Axis t3 = new Axis();
			t3.Id = "T3";
			layout.AddAxis(t3);
			t3.TemporaryLimits.Add(40, 0L, 500L);

			TopoTemporaryLimits.Apply(layout, Array.Empty<TemporarySpeedLimit>());

			Assert.Null(t3.GetTemporarySpeedLimit(100L));
			Assert.Equal(0, t3.TemporaryLimits.SpeedCount);
		}
	}
}
