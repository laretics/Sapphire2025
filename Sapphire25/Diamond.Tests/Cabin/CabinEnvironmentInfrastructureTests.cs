using Diamond.Cabin;
using Diamond.Topo;

namespace Diamond.Tests.Cabin
{
	public class CabinEnvironmentInfrastructureTests
	{
		[Fact]
		public void Load_AppliesPalmaEnllacDoubleTrack()
		{
			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			Axis t3 = topo.FindAxisById("T3")!;
			Assert.Equal(1, t3.GetTrackCountAt(1000L));

			CabinEnvironment cabin = new CabinEnvironment();
			cabin.Load(
				topo,
				Guid.Empty,
				string.Empty,
				package: null,
				publishedPlanId: Guid.Empty,
				publishedPlanName: "test",
				validFrom: null,
				validTo: null);

			Assert.Same(topo, cabin.Topo);
			Assert.Equal(2, t3.GetTrackCountAt(1000L));
			Assert.Equal(2, t3.GetTrackCountAt(30000L));
			Assert.Equal(1, t3.GetTrackCountAt(40000L));
		}
	}
}
