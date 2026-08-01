using Diamond.Controls.Rendering;
using Diamond.Timed;
using Diamond.Topo;

namespace Diamond.Tests.Controls
{
	public class StationTimeLabelsTests
	{
		[Fact]
		public void Origin_ShowsDepartureOnly()
		{
			Circulation c = PlanOneTripWithStops();
			long originPk = c.Asimilation.Origin.PK;

			Assert.True(StationTimeLabels.TryCreate(c, originPk, out StationTimeLabels.Annotation ann));
			Assert.Equal(StationTimeLabels.Kind.Origin, ann.Kind);
			Assert.True(ann.IsStopStyle);
			Assert.Equal(StationTimeLabels.FormatClock(c.Departure), ann.Text);
			Assert.DoesNotContain("·", ann.Text, StringComparison.Ordinal);
			Assert.DoesNotContain("–", ann.Text, StringComparison.Ordinal);
		}

		[Fact]
		public void Destination_ShowsArrivalOnly()
		{
			Circulation c = PlanOneTripWithStops();
			long destPk = c.Asimilation.Destination.PK;

			Assert.True(StationTimeLabels.TryCreate(c, destPk, out StationTimeLabels.Annotation ann));
			Assert.Equal(StationTimeLabels.Kind.Destination, ann.Kind);
			Assert.True(ann.IsStopStyle);
			Assert.Equal(StationTimeLabels.FormatClock(c.Arrival), ann.Text);
		}

		[Fact]
		public void MomentaryStop_ShowsSingleTimeAndDot()
		{
			// Default dwell 30s en A,M,B path → M es momentánea (< 1 min).
			Circulation c = PlanOneTripWithStops(defaultDwell: "30s", longDwellStation: null);
			long midPk = 10000L;

			Assert.True(StationTimeLabels.TryCreate(c, midPk, out StationTimeLabels.Annotation ann));
			Assert.Equal(StationTimeLabels.Kind.Momentary, ann.Kind);
			Assert.True(ann.IsStopStyle);
			Assert.EndsWith(" ·", ann.Text);
			Assert.DoesNotContain("–", ann.Text, StringComparison.Ordinal);
		}

		[Fact]
		public void CommercialStop_ShowsArrivalAndDeparture()
		{
			Circulation c = PlanOneTripWithStops(defaultDwell: "30s", longDwellStation: "M", longDwell: "5min");
			long midPk = 10000L;

			Assert.True(StationTimeLabels.TryCreate(c, midPk, out StationTimeLabels.Annotation ann));
			Assert.Equal(StationTimeLabels.Kind.Commercial, ann.Kind);
			Assert.True(ann.IsStopStyle);
			Assert.Contains("–", ann.Text, StringComparison.Ordinal);
			Assert.NotNull(ann.Arrival);
			Assert.NotNull(ann.Departure);
			Assert.True(ann.Departure!.Value - ann.Arrival!.Value >= TimeSpan.FromMinutes(4.5));
			Assert.Equal(
				StationTimeLabels.FormatClock(ann.Arrival.Value)
				+ "–"
				+ StationTimeLabels.FormatClock(ann.Departure.Value),
				ann.Text);
		}

		[Fact]
		public void PassThrough_ShowsPassTimeNotStopStyle()
		{
			// Solo principales en el patrón; H no está en la malla de paradas si no es principal
			// y no se fuerza con stops. Construimos un apeadero intermedio que se salta.
			// A -- H(halt) -- M -- B; stops 30s skip H → H es paso.
			Circulation c = PlanTripWithSkippedHalt();
			long haltPk = 5000L;

			Assert.True(StationTimeLabels.TryCreate(c, haltPk, out StationTimeLabels.Annotation ann));
			Assert.Equal(StationTimeLabels.Kind.Pass, ann.Kind);
			Assert.False(ann.IsStopStyle);
			Assert.DoesNotContain("·", ann.Text, StringComparison.Ordinal);
			Assert.DoesNotContain("–", ann.Text, StringComparison.Ordinal);
		}

		[Fact]
		public void OutsidePath_ReturnsFalse()
		{
			Circulation c = PlanOneTripWithStops();
			Assert.False(StationTimeLabels.TryCreate(c, 999_999L, out _));
		}

		[Fact]
		public void MultiAxis_ReverseDisplayView_ShowsOriginDepartureAtSpb()
		{
			// Catálogo UI T3+T2 = Palma→SPB (PK 0 en PMI).
			// Tren SPB→PMI: en su vista PK 0 es SPB; en la UI SPB está al otro extremo.
			// Sin proyectar, mark.Pk de SPB se confunde con el destino y muestra la llegada.
			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			SfmDemoInfrastructure.Apply(topo);

			Plan plan = new Plan(topo);
			plan.EnsureDefaultTrainSpecs();
			plan.DemandScript = """
				days lab
				  req SPB -> PMI 07:15
				    stops 30s
				""";
			Assert.True(plan.CompileDemand().Success, string.Join("; ", plan.CompileDemand().Errors));
			Mesh mesh = new MeshPlanner(plan).Solve(DayOfWeek.Monday);
			Assert.NotEmpty(mesh.Circulations);
			Circulation c = mesh.Circulations[0];
			Assert.Equal(TimeSpan.FromHours(7) + TimeSpan.FromMinutes(15), c.Departure);

			RouteView ui = BuildPalmaSpbCatalogView(topo);
			Assert.True(ui.IsReversePath(c.Asimilation.View) || ui.IsSameOrReversePath(c.Asimilation.View));

			StationOnRoute? spbUi = null;
			int i = 0;
			while (i < ui.Stations.Count)
			{
				if (string.Equals(ui.Stations[i].Station.Avr, "SPB", StringComparison.OrdinalIgnoreCase))
				{
					spbUi = ui.Stations[i];
					break;
				}

				i++;
			}

			Assert.NotNull(spbUi);
			// Sin mapear (bug antiguo): PK alto de SPB en UI = destino del tren → Arrival.
			Assert.True(StationTimeLabels.TryCreate(c, spbUi!.PK, out StationTimeLabels.Annotation wrong));
			// Con la vista de pantalla: debe ser salida 07:15 en origen.
			Assert.True(
				StationTimeLabels.TryCreate(c, ui, spbUi.PK, out StationTimeLabels.Annotation ann),
				"debe proyectar SPB UI → origen del tren");
			Assert.Equal(StationTimeLabels.Kind.Origin, ann.Kind);
			Assert.Equal("07:15", ann.Text);
			Assert.NotEqual(wrong.Text, ann.Text);
		}

		private static RouteView BuildPalmaSpbCatalogView(TopoLayout topo)
		{
			Axis t3 = topo.FindAxisById("T3")!;
			Axis t2 = topo.FindAxisById("T2")!;
			StationOnAxis palma = null!;
			StationOnAxis enllacT3 = null!;
			StationOnAxis enllacT2 = null!;
			StationOnAxis spb = null!;
			int i = 0;
			while (i < t3.Stations.Count)
			{
				StationOnAxis p = t3.Stations[i];
				if (string.Equals(p.Station.Avr, "PMI", StringComparison.OrdinalIgnoreCase))
				{
					palma = p;
				}

				if (p.Station.Name.IndexOf("Enlla", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					enllacT3 = p;
				}

				i++;
			}

			i = 0;
			while (i < t2.Stations.Count)
			{
				StationOnAxis p = t2.Stations[i];
				if (p.Station.Name.IndexOf("Enlla", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					enllacT2 = p;
				}

				if (string.Equals(p.Station.Avr, "SPB", StringComparison.OrdinalIgnoreCase))
				{
					spb = p;
				}

				i++;
			}

			List<(Axis Axis, long FromPk, long ToPk)> segs = new List<(Axis, long, long)>();
			segs.Add((t3, palma.PK, enllacT3.PK));
			segs.Add((t2, enllacT2.PK, spb.PK));
			return RouteView.Concat("T3+T2", "Palma → Sa Pobla", segs);
		}

		private static Circulation PlanOneTripWithStops(
			string defaultDwell = "30s",
			string? longDwellStation = "M",
			string longDwell = "5min")
		{
			Plan plan = CreateCorridorPlan(withHalt: false);
			string script = $"""
				req A -> B 06:00-08:00 as R1
				  stops {defaultDwell}
				""";
			if (!string.IsNullOrEmpty(longDwellStation))
			{
				script += $"\n  dwell {longDwellStation} {longDwell}";
			}

			plan.DemandScript = script;
			Assert.True(plan.CompileDemand().Success, "compile");
			Mesh mesh = new MeshPlanner(plan).Solve(DayOfWeek.Monday);
			Assert.NotEmpty(mesh.Circulations);
			return mesh.Circulations[0];
		}

		private static Circulation PlanTripWithSkippedHalt()
		{
			Plan plan = CreateCorridorPlan(withHalt: true);
			plan.DemandScript = """
				req A -> B 06:00-08:00 as R1
				  stops 30s
				  skip H
				""";
			Assert.True(plan.CompileDemand().Success, "compile");
			Mesh mesh = new MeshPlanner(plan).Solve(DayOfWeek.Monday);
			Assert.NotEmpty(mesh.Circulations);
			return mesh.Circulations[0];
		}

		private static Plan CreateCorridorPlan(bool withHalt)
		{
			// AVR en mayúsculas → principal; halt con avr minúsculas / no principal.
			Station stA = MakeStation("A", "STA");
			Station stH = MakeStation("H", "h1");
			Station stM = MakeStation("M", "STM");
			Station stB = MakeStation("B", "STB");

			Axis axis = new Axis();
			axis.Id = "X1";
			axis.Vmax = 100;

			AxisVertex v0 = new AxisVertex(39.0, 2.0, 0L);
			v0.Station = stA;
			axis.AddVertex(v0);

			if (withHalt)
			{
				AxisVertex vH = new AxisVertex(39.025, 2.025, 5000L);
				vH.Station = stH;
				axis.AddVertex(vH);
			}

			AxisVertex v1 = new AxisVertex(39.05, 2.05, 10000L);
			v1.Station = stM;
			axis.AddVertex(v1);
			AxisVertex v2 = new AxisVertex(39.1, 2.1, 20000L);
			v2.Station = stB;
			axis.AddVertex(v2);
			axis.Rebuild();
			axis.SetCantonFrontiers(withHalt
				? new long[] { 0L, 5000L, 10000L, 20000L }
				: new long[] { 0L, 10000L, 20000L });
			axis.DefaultTrackCount = 2;

			TopoLayout topo = new TopoLayout();
			topo.AddStation(stA);
			if (withHalt)
			{
				topo.AddStation(stH);
			}

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
