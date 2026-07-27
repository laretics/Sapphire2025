using Diamond.Timed;
using Diamond.Topo;

namespace Diamond.Tests.Timed
{
	/// <summary>
	/// Trenes PMI↔SPB (ida y vuelta) deben ser visibles en la vista multi-eje de catálogo T3+T2.
	/// </summary>
	public class PalmaSpbVisibilityTests
	{
		[Fact]
		public void PalmaSpb_BothDirections_VisibleOnCatalogView()
		{
			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			SfmDemoInfrastructure.Apply(topo);

			RouteView ui = BuildPalmaSpbCatalogView(topo);
			string script = """
				plan "SFM Palma-Sa Pobla"
				require both ways every 60 min PMI -> SPB 06:00-21:00 as R-SPB
				  days lab
				  stops 30s
				  dwell INC 1min
				""";

			Plan plan = new Plan(topo);
			plan.EnsureDefaultTrainSpecs();
			Assert.True(plan.CompileDemand(script).Success, "compile");

			Mesh mesh = new MeshPlanner(plan).Solve(DayOfWeek.Monday);
			Assert.True(mesh.Circulations.Count > 0, "debe haber circulaciones");

			int visible = 0;
			List<string> hidden = new List<string>();
			int i = 0;
			while (i < mesh.Circulations.Count)
			{
				Circulation c = mesh.Circulations[i];
				bool vis = MeshCantonGeometry.IsVisibleOnView(c.Asimilation, ui);
				if (vis)
				{
					visible++;
				}
				else
				{
					hidden.Add(
						c.Id + " sig=" + c.Asimilation.View.PathSignature()
						+ " ui=" + ui.PathSignature()
						+ " " + c.Asimilation.Origin.Station.Avr
						+ "->" + c.Asimilation.Destination.Station.Avr);
				}

				i++;
			}

			Assert.True(
				visible == mesh.Circulations.Count,
				"Solo " + visible + "/" + mesh.Circulations.Count + " visibles en T3+T2. "
				+ string.Join(" || ", hidden.Take(8)));
		}

		[Fact]
		public void MultiAxis_OppositeTrains_ArePhysicallyOpposite_EvenIfBothIncreasingOnOwnView()
		{
			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			SfmDemoInfrastructure.Apply(topo);

			string script = """
				require both ways every 60 min PMI -> SPB 06:00-10:00 as R-SPB
				  days lab
				  stops 30s
				""";

			Plan plan = new Plan(topo);
			plan.EnsureDefaultTrainSpecs();
			Assert.True(plan.CompileDemand(script).Success);

			Mesh mesh = new MeshPlanner(plan).Solve(DayOfWeek.Monday);
			Circulation? forward = null;
			Circulation? ret = null;
			int i = 0;
			while (i < mesh.Circulations.Count)
			{
				Circulation c = mesh.Circulations[i];
				if (string.Equals(c.Asimilation.Origin.Station.Avr, "PMI", StringComparison.OrdinalIgnoreCase))
				{
					forward = c;
				}

				if (string.Equals(c.Asimilation.Origin.Station.Avr, "SPB", StringComparison.OrdinalIgnoreCase))
				{
					ret = c;
				}

				i++;
			}

			Assert.NotNull(forward);
			Assert.NotNull(ret);

			// Ambas asimilaciones suelen ser Increasing en su vista propia, pero en el terreno son opuestas.
			Assert.True(
				MeshCantonGeometry.ArePhysicallyOpposite(forward!.Asimilation, ret!.Asimilation),
				"Ida PMI→SPB y vuelta SPB→PMI deben ser físicamente opuestas");

			// Cruce en zona Palma–Enllaç (doble vía T3): no debe reportarse conflicto duro
			// solo por solaparse en tiempo en ese tramo.
			RouteView ui = BuildPalmaSpbCatalogView(topo);
			// Comprobar que en el tramo de doble vía el max tracks es 2
			StationOnAxis enllac = topo.FindAxisById("T3")!.Stations.First(s =>
				s.Station.Name.Contains("Enlla", StringComparison.OrdinalIgnoreCase));
			Assert.True(ui.GetTrackCountAt(enllac.PK) >= 2 || ui.GetTrackCountAt(Math.Max(0, enllac.PK - 1)) >= 2);
		}

		[Fact]
		public void T3Corridor_Trains_VisibleOnPalmaSpbView_CommonSegment()
		{
			// Malla solo T3 (PMI–MAN): al mirar T3+T2 deben verse en Palma–Enllaç.
			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			SfmDemoInfrastructure.Apply(topo);
			RouteView ui = BuildPalmaSpbCatalogView(topo);

			string script = """
				plan "SFM T3"
				require both ways every 60 min PMI -> MAN 06:00-10:00 as R-T3
				  days lab
				  stops 30s
				""";

			Plan plan = new Plan(topo);
			plan.EnsureDefaultTrainSpecs();
			Assert.True(plan.CompileDemand(script).Success, "compile");

			Mesh mesh = new MeshPlanner(plan).Solve(DayOfWeek.Monday);
			Assert.True(mesh.Circulations.Count > 0);

			int visible = 0;
			int i = 0;
			while (i < mesh.Circulations.Count)
			{
				if (MeshCantonGeometry.IsVisibleOnView(mesh.Circulations[i].Asimilation, ui))
				{
					visible++;
				}

				i++;
			}

			Assert.True(visible > 0, "Los trenes T3 deben ser visibles en T3+T2 (tramo común).");
			Assert.Equal(mesh.Circulations.Count, visible);

			// Proyección: un PK de T3 cerca de Palma cae en la vista; más allá de Enllaç no.
			Circulation sample = mesh.Circulations[0];
			long nearPalma = sample.Asimilation.Origin.PK;
			long displayPk;
			Assert.True(
				ui.TryMapRoutePkFrom(sample.Asimilation.View, nearPalma, out displayPk),
				"origen T3 debe proyectarse en T3+T2");

			// PK de destino MAN no está en T3+T2
			long manPk = sample.Asimilation.Destination.PK;
			// Según sentido: destino puede ser MAN o PMI
			if (string.Equals(sample.Asimilation.Destination.Station.Avr, "MAN", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(sample.Asimilation.Origin.Station.Avr, "MAN", StringComparison.OrdinalIgnoreCase))
			{
				long manRoutePk = string.Equals(sample.Asimilation.Destination.Station.Avr, "MAN", StringComparison.OrdinalIgnoreCase)
					? sample.Asimilation.Destination.PK
					: sample.Asimilation.Origin.PK;
				long mapped;
				// Puede fallar (fuera del tramo T3 de la vista) o mapear solo si Enllaç-MAN no está en vista.
				bool ok = ui.TryMapRoutePkFrom(sample.Asimilation.View, manRoutePk, out mapped);
				// Enllaç está a ~33573; MAN más lejos → no debe mapear.
				Assert.False(ok, "MAN no forma parte de T3+T2");
			}
		}

		/// <summary>Misma construcción que DemoMeshService.BuildDemoViews para T3+T2.</summary>
		private static RouteView BuildPalmaSpbCatalogView(TopoLayout topo)
		{
			Axis t3 = topo.FindAxisById("T3")!;
			Axis t2 = topo.FindAxisById("T2")!;
			StationOnAxis palma = t3.Stations.First(s =>
				string.Equals(s.Station.Avr, "PMI", StringComparison.OrdinalIgnoreCase)
				|| s.Station.Id == "01");
			StationOnAxis enllacT3 = t3.Stations.First(s =>
				s.Station.Name.Contains("Enlla", StringComparison.OrdinalIgnoreCase));
			StationOnAxis enllacT2 = t2.Stations.First(s =>
				s.Station.Name.Contains("Enlla", StringComparison.OrdinalIgnoreCase));
			StationOnAxis spb = t2.Stations.First(s =>
				string.Equals(s.Station.Avr, "SPB", StringComparison.OrdinalIgnoreCase)
				|| s.Station.Id == "33");

			List<(Axis Axis, long FromPk, long ToPk)> segs = new List<(Axis, long, long)>();
			segs.Add((t3, palma.PK, enllacT3.PK));
			segs.Add((t2, enllacT2.PK, spb.PK));
			return RouteView.Concat("T3+T2", "Palma → Sa Pobla", segs);
		}
	}
}
