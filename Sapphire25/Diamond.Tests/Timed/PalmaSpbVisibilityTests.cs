using Diamond.Controls.Rendering;
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
		public void T3Manacor_Trains_MapUpToEnllac_OnPalmaSpbView()
		{
			// PMI→MAN en vista T3+T2: la traza debe llegar hasta Enllaç (fin del tramo T3 común),
			// no cortarse en la parada anterior (p. ej. Inca).
			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			SfmDemoInfrastructure.Apply(topo);
			RouteView ui = BuildPalmaSpbCatalogView(topo);

			Axis t3 = topo.FindAxisById("T3")!;
			StationOnAxis enllacT3 = t3.Stations.First(s =>
				s.Station.Name.Contains("Enlla", StringComparison.OrdinalIgnoreCase));
			StationOnAxis palma = t3.Stations.First(s =>
				string.Equals(s.Station.Avr, "PMI", StringComparison.OrdinalIgnoreCase) || s.Station.Id == "01");
			long enllacOnUi = enllacT3.PK - palma.PK; // Concat: route PK 0 en Palma

			// Enllaç debe ser estación de la vista y mapearse desde T3 completo.
			StationOnRoute? enllacUi = ui.Stations.FirstOrDefault(s =>
				s.Station.Name.Contains("Enlla", StringComparison.OrdinalIgnoreCase));
			Assert.NotNull(enllacUi);

			RouteView t3View = RouteView.FromAxis(t3);
			long mappedEnllac;
			Assert.True(
				ui.TryMapRoutePkFrom(t3View, enllacT3.PK, out mappedEnllac),
				"Enllaç en T3 debe proyectarse en T3+T2");
			Assert.Equal(enllacUi!.PK, mappedEnllac);

			// Como en la demanda demo: Enllaç en skip → no hay key de parada en Enllaç;
			// la traza debe llegar igual al PK de Enllaç por el extremo del tramo mapeable.
			string script = """
				require both ways every 60 min PMI -> MAN 06:00-10:00 as R-T3
				  days lab
				  stops 30s
				  skip RLL Enllaç "Sant Joan" PSJ
				  dwell INC 1min
				""";
			Plan plan = new Plan(topo);
			plan.EnsureDefaultTrainSpecs();
			Assert.True(plan.CompileDemand(script).Success);
			Mesh mesh = new MeshPlanner(plan).Solve(DayOfWeek.Monday);

			// Ida PMI→MAN
			Circulation? toMan = mesh.Circulations.FirstOrDefault(c =>
				string.Equals(c.Asimilation.Origin.Station.Avr, "PMI", StringComparison.OrdinalIgnoreCase)
				&& string.Equals(c.Asimilation.Destination.Station.Avr, "MAN", StringComparison.OrdinalIgnoreCase));
			Assert.NotNull(toMan);

			long maxDisp = long.MinValue;
			int s = 0;
			while (s <= 200)
			{
				double u = s / 200.0;
				long apk = toMan!.Asimilation.PKByTime(
					TimeSpan.FromSeconds(u * toMan.Asimilation.TotalTime.TotalSeconds));
				long dpk;
				if (ui.TryMapRoutePkFrom(toMan.Asimilation.View, apk, out dpk))
				{
					if (dpk > maxDisp)
					{
						maxDisp = dpk;
					}
				}

				s++;
			}

			// Debe llegar al PK de Enllaç en la vista (tolerancia 500 m por muestreo).
			Assert.True(
				maxDisp >= enllacUi.PK - 500L,
				"PMI→MAN solo llega a display PK=" + maxDisp
				+ " pero Enllaç está en " + enllacUi.PK
				+ " (delta=" + (enllacUi.PK - maxDisp) + ")");

			// El builder de traza (mismo camino que el SVG) debe llegar a Enllaç aunque
			// Enllaç esté en skip (último apeadero comercial en Inca).
			MeshYScale yScale = MeshYScale.Create(MeshYScaleMode.LinearPk, ui, ui.PK, ui.PKEnd);
			List<MeshTrainPathBuilder.Point> pts = MeshTrainPathBuilder.CollectControlPoints(
				toMan!, ui, ui.PK, ui.PKEnd,
				toMan!.Departure.TotalSeconds - 60, toMan.Arrival.TotalSeconds + 60,
				40, 36, 800, 600, yScale, 64, false, out _, out _);
			Assert.True(pts.Count >= 4, "puntos de control PMI→MAN");

			// Reconstruir el max PK display a partir de los puntos Y (Y baja con PK↑).
			double yEnllac = yScale.PkToY(enllacUi.PK, 36, 600);
			double yInca = double.NaN;
			StationOnRoute? incaUi = ui.Stations.FirstOrDefault(s =>
				string.Equals(s.Station.Avr, "INC", StringComparison.OrdinalIgnoreCase)
				|| s.Station.Name.Contains("Inca", StringComparison.OrdinalIgnoreCase));
			if (incaUi is not null)
			{
				yInca = yScale.PkToY(incaUi.PK, 36, 600);
			}

			double minY = pts[0].Y;
			int pi = 1;
			while (pi < pts.Count)
			{
				if (pts[pi].Y < minY)
				{
					minY = pts[pi].Y;
				}

				pi++;
			}

			// Debe bajar al menos hasta cerca de Enllaç (no quedarse en Inca).
			Assert.True(
				minY <= yEnllac + 8.0,
				"traza PMI→MAN en T3+T2 se corta antes de Enllaç: minY=" + minY.ToString("F1")
				+ " yEnllac=" + yEnllac.ToString("F1")
				+ (double.IsNaN(yInca) ? "" : " yInca=" + yInca.ToString("F1"))
				+ " pts=" + pts.Count);
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

		[Fact]
		public void TryFindPath_And_CatalogConcat_AreSameOrMappable()
		{
			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			SfmDemoInfrastructure.Apply(topo);
			RouteView catalog = BuildPalmaSpbCatalogView(topo);

			Station palma = topo.Stations.First(s =>
				string.Equals(s.Avr, "PMI", StringComparison.OrdinalIgnoreCase) || s.Id == "01");
			Station spb = topo.Stations.First(s =>
				string.Equals(s.Avr, "SPB", StringComparison.OrdinalIgnoreCase) || s.Id == "33");

			RouteView? planned;
			Assert.True(RouteView.TryFindPath(topo, palma, spb, out planned, out _, out _));
			Assert.NotNull(planned);

			Console.WriteLine("catalog=" + catalog.PathSignature());
			Console.WriteLine("planned=" + planned!.PathSignature());
			Console.WriteLine("same=" + catalog.IsSamePath(planned) + " rev=" + catalog.IsReversePath(planned));
			Console.WriteLine("catalog len=" + catalog.Length + " planned len=" + planned.Length);

			// Aunque la firma difiera, la proyección punto a punto debe cubrir el trayecto.
			Assert.True(
				catalog.IsSamePath(planned)
				|| catalog.IsReversePath(planned)
				|| catalog.OverlapsPhysically(planned),
				"catálogo y camino planificado deben ser el mismo corredor");
		}

		[Fact]
		public void PalmaSpb_PathPoints_CoverNearlyFullCatalogViewPk()
		{
			// Las trazas PMI↔SPB deben proyectarse casi de PK0 a PKEnd en la vista T3+T2.
			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			SfmDemoInfrastructure.Apply(topo);
			RouteView ui = BuildPalmaSpbCatalogView(topo);

			string script = """
				require both ways every 60 min PMI -> SPB 06:00-12:00 as R-SPB
				  days lab
				  stops 30s
				""";

			Plan plan = new Plan(topo);
			plan.EnsureDefaultTrainSpecs();
			Assert.True(plan.CompileDemand(script).Success, "compile");
			Mesh mesh = new MeshPlanner(plan).Solve(DayOfWeek.Monday);
			Assert.True(mesh.Circulations.Count >= 2);

			MeshYScale yScale = MeshYScale.Create(MeshYScaleMode.LinearPk, ui, ui.PK, ui.PKEnd);
			long uiSpan = ui.PKEnd - ui.PK;
			Assert.True(uiSpan > 10000L);

			int i = 0;
			int checkedN = 0;
			while (i < mesh.Circulations.Count && checkedN < 4)
			{
				Circulation c = mesh.Circulations[i];
				Assert.True(MeshCantonGeometry.IsVisibleOnView(c.Asimilation, ui), c.Id + " visible");

				// Muestrear el trayecto completo en PK de asimilación y proyectar.
				long minDisp = long.MaxValue;
				long maxDisp = long.MinValue;
				int ok = 0;
				int fail = 0;
				int s = 0;
				while (s <= 20)
				{
					double u = s / 20.0;
					long asimPk = c.Asimilation.PKByTime(
						TimeSpan.FromSeconds(u * c.Asimilation.TotalTime.TotalSeconds));
					long dispPk;
					if (ui.TryMapRoutePkFrom(c.Asimilation.View, asimPk, out dispPk))
					{
						ok++;
						if (dispPk < minDisp)
						{
							minDisp = dispPk;
						}

						if (dispPk > maxDisp)
						{
							maxDisp = dispPk;
						}
					}
					else
					{
						fail++;
					}

					s++;
				}

				Assert.True(fail == 0, c.Id + " fallos de mapeo=" + fail + " sigAsim="
					+ c.Asimilation.View.PathSignature() + " sigUi=" + ui.PathSignature()
					+ " same=" + ui.IsSamePath(c.Asimilation.View)
					+ " rev=" + ui.IsReversePath(c.Asimilation.View));

				long covered = maxDisp - minDisp;
				// Debe cubrir al menos el 85 % de la vista (extremos en estaciones).
				Assert.True(
					covered * 100 >= uiSpan * 85,
					c.Id + " cobertura PK display " + minDisp + ".." + maxDisp
					+ " (" + covered + " m) vs vista " + ui.PK + ".." + ui.PKEnd
					+ " (" + uiSpan + " m). same=" + ui.IsSamePath(c.Asimilation.View)
					+ " rev=" + ui.IsReversePath(c.Asimilation.View)
					+ " asimSig=" + c.Asimilation.View.PathSignature()
					+ " uiSig=" + ui.PathSignature());

				// Control points del builder deben existir y cubrir buen rango Y.
				List<Diamond.Controls.Rendering.MeshTrainPathBuilder.Point> pts =
					Diamond.Controls.Rendering.MeshTrainPathBuilder.CollectControlPoints(
						c, ui, ui.PK, ui.PKEnd,
						c.Departure.TotalSeconds - 60, c.Arrival.TotalSeconds + 60,
						40, 36, 800, 600, yScale, 96, false, out _, out _);
				Assert.True(pts.Count >= 8, c.Id + " puntos=" + pts.Count);
				double minY = pts[0].Y;
				double maxY = pts[0].Y;
				int p = 1;
				while (p < pts.Count)
				{
					if (pts[p].Y < minY)
					{
						minY = pts[p].Y;
					}

					if (pts[p].Y > maxY)
					{
						maxY = pts[p].Y;
					}

					p++;
				}

				// plotH=600: un trayecto casi completo debe ocupar buen tramo vertical.
				Assert.True(
					maxY - minY >= 400.0,
					c.Id + " rango Y demasiado corto: " + minY.ToString("F1") + ".." + maxY.ToString("F1"));

				checkedN++;
				i++;
			}

			Assert.True(checkedN >= 2);
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
