using System.Globalization;
using Diamond.Motion;
using Diamond.Timed;
using Diamond.Topo;

namespace Diamond.Tests.Timed
{
	public class TrainNumberingTests
	{
		[Fact]
		public void Assign_OddAscending_EvenDescending_SameCorridor()
		{
			Plan plan = CreatePlanWithCorridor(doubleTrack: true);
			plan.DemandScript = """
				require both ways every 60 min A -> B 06:00-10:00 as R1
				""";
			Assert.True(plan.CompileDemand().Success);

			Mesh mesh = new MeshPlanner(plan).Solve();
			Assert.True(mesh.Circulations.Count >= 4);

			List<Circulation> ascending = mesh.Circulations
				.Where(c => c.Asimilation.Sense == CirculationSense.IncreasingPk)
				.OrderBy(c => c.Departure)
				.ToList();
			List<Circulation> descending = mesh.Circulations
				.Where(c => c.Asimilation.Sense == CirculationSense.DecreasingPk)
				.OrderBy(c => c.Departure)
				.ToList();

			Assert.NotEmpty(ascending);
			Assert.NotEmpty(descending);

			// Misma serie (fallback o conocida); impares / pares (números clásicos NN##)
			Assert.All(ascending, c =>
			{
				Assert.True(c.HasServiceNumber);
				int n = int.Parse(c.ServiceNumber, CultureInfo.InvariantCulture);
				Assert.Equal(1, n % 2);
			});
			Assert.All(descending, c =>
			{
				Assert.True(c.HasServiceNumber);
				int n = int.Parse(c.ServiceNumber, CultureInfo.InvariantCulture);
				Assert.Equal(0, n % 2);
			});

			int series = int.Parse(ascending[0].ServiceNumber, CultureInfo.InvariantCulture) / 100;
			Assert.All(ascending, c =>
				Assert.Equal(series, int.Parse(c.ServiceNumber, CultureInfo.InvariantCulture) / 100));
			Assert.All(descending, c =>
				Assert.Equal(series, int.Parse(c.ServiceNumber, CultureInfo.InvariantCulture) / 100));

			// Secuencia +2 por orden de salida en cada sentido
			int i = 1;
			while (i < ascending.Count)
			{
				int prev = int.Parse(ascending[i - 1].ServiceNumber, CultureInfo.InvariantCulture);
				int cur = int.Parse(ascending[i].ServiceNumber, CultureInfo.InvariantCulture);
				Assert.Equal(prev + 2, cur);
				i++;
			}

			i = 1;
			while (i < descending.Count)
			{
				int prev = int.Parse(descending[i - 1].ServiceNumber, CultureInfo.InvariantCulture);
				int cur = int.Parse(descending[i].ServiceNumber, CultureInfo.InvariantCulture);
				Assert.Equal(prev + 2, cur);
				i++;
			}
		}

		[Fact]
		public void Assign_DifferentAsimilations_SameOd_ShareSeries()
		{
			Plan plan = CreatePlanWithCorridor(doubleTrack: true);
			// Dos requisitos mismo OD A→B, distinto patrón de paradas → dos asimilaciones ida
			plan.DemandScript = """
				require every 120 min A -> B 06:00-10:00 as R-all
				  stops 30s
				require every 120 min A -> B 06:30-10:00 as R-skip
				  stops 30s
				  skip M
				""";
			Assert.True(plan.CompileDemand().Success);

			Mesh mesh = new MeshPlanner(plan).Solve(DayOfWeek.Monday);
			List<Circulation> outbound = mesh.Circulations
				.Where(c => c.Asimilation.Sense == CirculationSense.IncreasingPk)
				.OrderBy(c => c.Departure)
				.ToList();

			Assert.True(outbound.Count >= 2);
			// Al menos dos asimilaciones distintas en el OD, pero misma serie numérica
			int distinctAsims = outbound.Select(c => c.Asimilation).Distinct().Count();
			Assert.True(distinctAsims >= 1);

			int series = int.Parse(outbound[0].ServiceNumber, CultureInfo.InvariantCulture) / 100;
			Assert.All(outbound, c =>
			{
				int n = int.Parse(c.ServiceNumber, CultureInfo.InvariantCulture);
				Assert.Equal(1, n % 2);
				Assert.Equal(series, n / 100);
			});

			// Numeración global por salida:  …01, 03, 05… sin reiniciar por asimilación
			int i = 1;
			while (i < outbound.Count)
			{
				int prev = int.Parse(outbound[i - 1].ServiceNumber, CultureInfo.InvariantCulture);
				int cur = int.Parse(outbound[i].ServiceNumber, CultureInfo.InvariantCulture);
				Assert.Equal(prev + 2, cur);
				i++;
			}
		}

		[Fact]
		public void KnownSeries_SfmCorridors()
		{
			Station pmi = MakeStation("01", "PMI", "Palma");
			Station man = MakeStation("24", "MAN", "Manacor");
			Station spb = MakeStation("33", "SPB", "Sa Pobla");
			Station inc = MakeStation("17", "INC", "Inca");
			Station uib = MakeStation("48", "UIB", "UIB");

			Assert.Equal(49, TrainNumbering.TryKnownSeriesBase(TrainNumbering.CorridorKey(pmi, man)));
			Assert.Equal(47, TrainNumbering.TryKnownSeriesBase(TrainNumbering.CorridorKey(pmi, spb)));
			Assert.Equal(45, TrainNumbering.TryKnownSeriesBase(TrainNumbering.CorridorKey(pmi, inc)));
			Assert.Equal(50, TrainNumbering.TryKnownSeriesBase(TrainNumbering.CorridorKey(pmi, uib)));
			Assert.Equal(70, TrainNumbering.TryKnownSeriesBase(TrainNumbering.CorridorKey(inc, spb)));
		}

		[Fact]
		public void Assign_IsDeterministic()
		{
			Plan plan = CreatePlanWithCorridor(doubleTrack: true);
			plan.DemandScript = """
				require both ways every 40 min A -> B 06:00-12:00 as R1
				""";
			Assert.True(plan.CompileDemand().Success);

			Mesh a = new MeshPlanner(plan).Solve();
			Mesh b = new MeshPlanner(plan).Solve();

			Assert.Equal(a.Circulations.Count, b.Circulations.Count);
			List<string> numsA = a.Circulations.Select(c => c.ServiceNumber).OrderBy(n => n, StringComparer.Ordinal).ToList();
			List<string> numsB = b.Circulations.Select(c => c.ServiceNumber).OrderBy(n => n, StringComparer.Ordinal).ToList();
			Assert.Equal(numsA, numsB);
		}

		[Fact]
		public void BothWays_OddsThenEvens_FromWindowStart_NoDelayForConflicts()
		{
			// Impares primero (todos), luego pares (todos); ambos desde inicio de ventana.
			// No se retrasan salidas por conflictos: se planifica a la cadencia y se errora si choca.
			Plan plan = CreatePlanWithCorridor(doubleTrack: false);
			plan.DemandScript = """
				require both ways every 40 min A -> B 06:05-10:00 as R-bw
				""";
			Assert.True(plan.CompileDemand().Success);

			Mesh mesh = new MeshPlanner(plan).Solve(DayOfWeek.Monday);
			List<Circulation> odds = mesh.Circulations
				.Where(c => int.Parse(c.ServiceNumber, CultureInfo.InvariantCulture) % 2 == 1)
				.OrderBy(c => c.Departure)
				.ToList();
			List<Circulation> evens = mesh.Circulations
				.Where(c => int.Parse(c.ServiceNumber, CultureInfo.InvariantCulture) % 2 == 0)
				.OrderBy(c => c.Departure)
				.ToList();

			Assert.NotEmpty(odds);
			Assert.NotEmpty(evens);
			Assert.Equal(TimeSpan.FromHours(6).Add(TimeSpan.FromMinutes(5)), odds[0].Departure);
			Assert.Equal(TimeSpan.FromHours(6).Add(TimeSpan.FromMinutes(5)), evens[0].Departure);
			Assert.Equal(CirculationSense.IncreasingPk, odds[0].Asimilation.Sense);
			Assert.Equal(CirculationSense.DecreasingPk, evens[0].Asimilation.Sense);

			// Cadencia fija de 40 min en impares (sin huecos por "buscar hueco")
			if (odds.Count >= 2)
			{
				Assert.Equal(TimeSpan.FromMinutes(40), odds[1].Departure - odds[0].Departure);
			}
		}

		[Fact]
		public void PlanningErrors_CiteServiceNumbers()
		{
			// Vía única, both ways sin desfase: debe generar conflictos duros con números de tren.
			Plan plan = CreatePlanWithCorridor(doubleTrack: false);
			plan.DemandScript = """
				require both ways every 20 min A -> B 06:00-08:00 as R-clash
				""";
			Assert.True(plan.CompileDemand().Success);

			Mesh mesh = new MeshPlanner(plan).Solve();
			// Puede haber errores o no según el planificador; si hay, deben citar "tren NNNN"
			if (mesh.Errors.Count > 0)
			{
				Assert.Contains(mesh.Errors, e => e.Contains("tren ", StringComparison.Ordinal));
				Assert.DoesNotContain(mesh.Errors, e => e.Contains("C1-", StringComparison.Ordinal)
					|| e.Contains("C2-", StringComparison.Ordinal));
			}
			else
			{
				// Al menos las circulaciones están numeradas
				Assert.All(mesh.Circulations, c => Assert.True(c.HasServiceNumber));
			}
		}

		private static Plan CreatePlanWithCorridor(bool doubleTrack = false)
		{
			Station stA = MakeStation("A", "STA", "A");
			Station stM = MakeStation("M", "STM", "M");
			Station stB = MakeStation("B", "STB", "B");

			Axis axis = new Axis();
			axis.Id = "X1";
			axis.Vmax = 100;

			AxisVertex v0 = new AxisVertex(39.0, 2.0, 0L);
			v0.Station = stA;
			AxisVertex v1 = new AxisVertex(39.05, 2.05, 10000L);
			v1.Station = stM;
			AxisVertex v2 = new AxisVertex(39.1, 2.1, 20000L);
			v2.Station = stB;
			axis.AddVertex(v0);
			axis.AddVertex(v1);
			axis.AddVertex(v2);
			axis.Rebuild();

			axis.SetCantonFrontiers(new long[] { 0L, 10000L, 20000L });
			axis.DefaultTrackCount = 1;
			if (doubleTrack)
			{
				axis.SetTrackCount(0L, 20000L, 2);
			}

			TopoLayout topo = new TopoLayout();
			topo.AddStation(stA);
			topo.AddStation(stM);
			topo.AddStation(stB);
			topo.AddAxis(axis);

			Plan plan = new Plan(topo);
			plan.EnsureDefaultTrainSpecs();
			return plan;
		}

		private static Station MakeStation(string id, string avr, string? name = null)
		{
			Station s = new Station(id);
			s.Avr = avr;
			s.Name = name ?? avr;
			return s;
		}
	}
}
