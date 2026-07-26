using Diamond.Timed;
using Diamond.Topo;

namespace Diamond.Tests.Timed
{
	public class SfmDemoInfrastructureTests
	{
		[Fact]
		public void PrincipalStation_UsesUppercaseAvr()
		{
			Assert.True(StationClassification.IsPrincipalAvr("PMI"));
			Assert.True(StationClassification.IsPrincipalAvr("MTX"));
			Assert.True(StationClassification.IsPrincipalAvr("ELÁ"));
			Assert.False(StationClassification.IsPrincipalAvr("jcv"));
			Assert.False(StationClassification.IsPrincipalAvr("cos"));
			Assert.False(StationClassification.IsPrincipalAvr("cla"));
		}

		[Fact]
		public void Apply_SfmSample_T3_HasDoubleTrackToEnllac_AndCantonsAtPrincipals()
		{
			TopoLayout layout = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			SfmDemoInfrastructure.Apply(layout);

			Axis t3 = layout.FindAxisById("T3")!;
			Assert.True(t3.CantonFrontiers.Count >= 2);

			// Palma y Marratxí (principales) deben ser frontera; apeadero jcv no.
			Assert.Contains(0L, t3.CantonFrontiers);
			Assert.Contains(8400L, t3.CantonFrontiers); // MTX
			Assert.DoesNotContain(618L, t3.CantonFrontiers); // jcv

			// Doble vía Palma–Enllaç (~33573).
			Assert.Equal(2, t3.GetTrackCountAt(1000L));
			Assert.Equal(2, t3.GetTrackCountAt(30000L));
			Assert.True(t3.AllowsLineCrossingAt(20000L));

			// Tras Enllaç, vía única (Manacor).
			Assert.Equal(1, t3.GetTrackCountAt(40000L));
			Assert.False(t3.AllowsLineCrossingAt(40000L));

			Axis m1 = layout.FindAxisById("M1")!;
			Assert.Equal(1, m1.GetTrackCountAt(1000L));
			Assert.Contains(0L, m1.CantonFrontiers);
			Assert.DoesNotContain(618L, m1.CantonFrontiers); // jcv apeadero
		}
	}
}
