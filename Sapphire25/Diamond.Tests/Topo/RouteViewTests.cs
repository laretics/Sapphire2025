using Diamond.Timed;
using Diamond.Topo;

namespace Diamond.Tests.Topo
{
	public class RouteViewTests
	{
		[Fact]
		public void FromAxis_PreservesAxisPkAsRoutePk()
		{
			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			SfmDemoInfrastructure.Apply(topo);
			Axis t3 = topo.FindAxisById("T3")!;

			RouteView view = RouteView.FromAxis(t3);

			Assert.Equal("T3", view.Id);
			Assert.Single(view.Legs);
			Assert.Equal(t3.PK, view.PK);
			Assert.Equal(t3.PKEnd, view.PKEnd);
			Assert.True(view.Stations.Count > 5);

			StationOnRoute? palma = view.FindStationByRef("01", "PMI", "Palma");
			Assert.NotNull(palma);
			Assert.Equal(0L, palma!.PK);
		}

		[Fact]
		public void Concat_PalmaSaPobla_TwoLegs()
		{
			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			SfmDemoInfrastructure.Apply(topo);
			Axis t3 = topo.FindAxisById("T3")!;
			Axis t2 = topo.FindAxisById("T2")!;

			StationOnAxis palma = t3.Stations.First(s => string.Equals(s.Station.Avr, "PMI", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(s.Station.Id, "01", StringComparison.Ordinal));
			StationOnAxis enllacT3 = t3.Stations.First(s => (s.Station.Name ?? string.Empty).Contains("Enlla", StringComparison.OrdinalIgnoreCase));
			StationOnAxis enllacT2 = t2.Stations.First(s => (s.Station.Name ?? string.Empty).Contains("Enlla", StringComparison.OrdinalIgnoreCase));
			StationOnAxis spb = t2.Stations.First(s => string.Equals(s.Station.Avr, "SPB", StringComparison.OrdinalIgnoreCase));

			List<(Axis, long, long)> segs = new List<(Axis, long, long)>();
			segs.Add((t3, palma.PK, enllacT3.PK));
			segs.Add((t2, enllacT2.PK, spb.PK));

			RouteView view = RouteView.Concat("T3+T2", "Palma → Sa Pobla", segs);

			Assert.Equal(2, view.Legs.Count);
			Assert.Equal(0L, view.PK);
			Assert.True(view.Length > 40000L);
			Assert.NotNull(view.FindStationByRef("01", "PMI", "Palma"));
			Assert.NotNull(view.FindStationByRef("33", "SPB", "Sa Pobla"));

			// Enllaç aparece una sola vez en la vista (nudo de enlace).
			int enllacCount = view.Stations.Count(s =>
				(s.Station.Name ?? string.Empty).Contains("Enlla", StringComparison.OrdinalIgnoreCase));
			Assert.Equal(1, enllacCount);
		}

		[Fact]
		public void TryFindPath_MultiAxis_PalmaToSaPobla()
		{
			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			SfmDemoInfrastructure.Apply(topo);

			Station? palma = topo.Stations.FirstOrDefault(s =>
				string.Equals(s.Avr, "PMI", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(s.Id, "01", StringComparison.Ordinal));
			Station? spb = topo.Stations.FirstOrDefault(s =>
				string.Equals(s.Avr, "SPB", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(s.Id, "33", StringComparison.Ordinal));

			Assert.NotNull(palma);
			Assert.NotNull(spb);

			RouteView? view;
			StationOnRoute? origin;
			StationOnRoute? destination;
			bool ok = RouteView.TryFindPath(topo, palma!, spb!, out view, out origin, out destination);

			Assert.True(ok);
			Assert.NotNull(view);
			Assert.NotNull(origin);
			Assert.NotNull(destination);
			Assert.True(view!.Legs.Count >= 2);
			Assert.Contains(view.Legs, leg => string.Equals(leg.Axis.Id, "T3", StringComparison.Ordinal));
			Assert.Contains(view.Legs, leg => string.Equals(leg.Axis.Id, "T2", StringComparison.Ordinal));
		}
	}
}
