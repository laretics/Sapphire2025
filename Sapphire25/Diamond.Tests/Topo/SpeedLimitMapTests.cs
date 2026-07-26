using Diamond.Topo;

namespace Diamond.Tests.Topo
{
	public class SpeedLimitMapTests
	{
		[Fact]
		public void GetMinSpeedAt_OverlappingSpeeds_ReturnsMostRestrictive()
		{
			SpeedLimitMap map = new SpeedLimitMap();
			map.Add(100, 0L, 1000L);
			map.Add(50, 400L, 600L);

			Assert.Equal(100, map.GetMinSpeedAt(100L));
			Assert.Equal(50, map.GetMinSpeedAt(500L));
			Assert.Equal(100, map.GetMinSpeedAt(700L));
			Assert.Null(map.GetMinSpeedAt(2000L));
		}

		[Fact]
		public void Axis_EffectiveSpeed_PrefersTemporaryWhenMoreRestrictive()
		{
			Axis axis = new Axis();
			axis.Vmax = 100;
			axis.FixedLimits.Add(80, 0L, 2000L);
			axis.TemporaryLimits.Add(30, 500L, 800L);

			Assert.Equal(80, axis.GetEffectiveSpeedLimit(100L));
			Assert.Equal(30, axis.GetEffectiveSpeedLimit(600L));
			Assert.Equal(80, axis.GetEffectiveSpeedLimit(900L));
			Assert.Equal(100, axis.GetEffectiveSpeedLimit(5000L)); // cae a Vmax
		}

		[Fact]
		public void Load_toposfm227_M1_FixedLimitsFromXml()
		{
			TopoLayout layout = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			Axis m1 = layout.FindAxisById("M1")!;

			// <item pk0="0" pkf="449" speed="30" .../>
			Assert.Equal(30, m1.FixedLimits.GetMinSpeedAt(0L));
			Assert.Equal(30, m1.FixedLimits.GetMinSpeedAt(448L));
			// Semántica [pk0, pkf): 449 ya no está en el tramo de 30.
			Assert.NotEqual(30, m1.FixedLimits.GetMinSpeedAt(449L));

			// Sin limitación fija en medio → efectiva = vmax 100
			Assert.Equal(100, m1.GetEffectiveSpeedLimit(2000L));

			// Puente 80 entre 6650 y 7250
			Assert.Equal(80, m1.GetEffectiveSpeedLimit(7000L));
		}
	}
}
