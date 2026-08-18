using Diamond.Controls.Rendering;
using Diamond.Timed;
using Diamond.Topo;

namespace Diamond.Tests.Controls
{
	public class ItineraryBookTests
	{
		[Fact]
		public void Build_FullMesh_HasCoverIndexAndTrains_HalfPagePagination()
		{
			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			SfmDemoInfrastructure.Apply(topo);
			Plan plan = new Plan(topo);
			plan.EnsureDefaultTrainSpecs();
			string script = """
				plan "SFM T3 test"
				notes "Prueba libro"
				require both ways every 60 min PMI -> MAN 06:00-10:00 as R-T3
				  days lab
				  stops 30s
				""";
			Assert.True(plan.CompileDemand(script).Success);
			Mesh mesh = new MeshPlanner(plan).Solve(DayOfWeek.Monday);
			Assert.True(mesh.Circulations.Count >= 2);

			ItineraryBookDocument book = ItineraryBookDocument.Build(
				mesh,
				planName: "SFM T3 test",
				notes: "Prueba libro",
				planningDay: DayOfWeek.Monday,
				maxFrontiersPerHalf: 30);

			// Portada + al menos un índice + al menos una semipágina de tren.
			Assert.True(book.HalfPages.Count >= 3, "halves=" + book.HalfPages.Count);
			Assert.Equal(ItineraryBookHalfKind.Cover, book.HalfPages[0].Kind);
			Assert.Equal(ItineraryBookHalfKind.Index, book.HalfPages[1].Kind);

			int cover = 0;
			int index = 0;
			int tables = 0;
			int i = 0;
			while (i < book.HalfPages.Count)
			{
				ItineraryBookHalfPage h = book.HalfPages[i];
				Assert.Equal(i + 1, h.PageNumber);
				Assert.Equal(book.TotalHalfPages, h.PageCount);
				if (h.Kind == ItineraryBookHalfKind.Cover)
				{
					cover++;
				}
				else if (h.Kind == ItineraryBookHalfKind.Index)
				{
					index++;
				}
				else
				{
					tables++;
				}

				i++;
			}

			Assert.Equal(1, cover);
			Assert.True(index >= 1);
			Assert.True(tables >= mesh.Circulations.Count);

			// 1 hoja física por cada 2 semipáginas.
			IReadOnlyList<string> sheets = CirculationSheetSvgRenderer.RenderAllBookSheets(book);
			Assert.Equal(book.PhysicalSheetCount, sheets.Count);
			Assert.Equal(CirculationSheetPager.ComputeSheetCount(book.TotalHalfPages), sheets.Count);
			Assert.Contains("LIBRO ITINERARIO", sheets[0], StringComparison.Ordinal);
			Assert.Contains("diamond-doc-logo", sheets[0], StringComparison.Ordinal);
			Assert.Contains("data:image/png;base64,", sheets[0], StringComparison.Ordinal);
			Assert.Contains("viewBox=\"0 0 841.89 595.28\"", sheets[0], StringComparison.Ordinal);
		}

		[Fact]
		public void Sort_AllOddsBeforeAllEvens_AcrossDirections()
		{
			// Aunque ida y vuelta son asimilaciones distintas, la numeración 49xx es común:
			// deben salir todos los impares y después todos los pares.
			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			SfmDemoInfrastructure.Apply(topo);
			Plan plan = new Plan(topo);
			plan.EnsureDefaultTrainSpecs();
			Assert.True(plan.CompileDemand("""
				require both ways every 40 min PMI -> MAN 06:00-12:00 as R-T3
				  days lab
				  stops 30s
				""").Success);
			Mesh mesh = new MeshPlanner(plan).Solve(DayOfWeek.Monday);
			ItineraryBookDocument book = ItineraryBookDocument.Build(mesh, "t", null, DayOfWeek.Monday, 30);

			List<long> numbers = new List<long>();
			int hi = 0;
			while (hi < book.HalfPages.Count)
			{
				ItineraryBookHalfPage h = book.HalfPages[hi];
				if (h.Kind == ItineraryBookHalfKind.TrainTable
					&& h.TrainDocument is not null
					&& h.TrainPage is not null
					&& h.TrainPage.PageIndex == 0)
				{
					// Primera semipágina de cada tren (evita repetir al multipágina).
					long n;
					if (ItineraryBookDocument.TryParseTrailingNumber(h.TrainDocument.TrainNumber, out n))
					{
						numbers.Add(n);
					}
				}

				hi++;
			}

			Assert.True(numbers.Count >= 4, "pocos trenes: " + numbers.Count);

			bool seenEven = false;
			int i = 0;
			while (i < numbers.Count)
			{
				bool isOdd = (numbers[i] % 2L) == 1L;
				if (isOdd)
				{
					Assert.False(seenEven, "impar " + numbers[i] + " después de un par en " + string.Join(",", numbers));
				}
				else
				{
					seenEven = true;
				}

				i++;
			}

			// Al menos un impar y un par en la malla both-ways.
			Assert.Contains(numbers, n => n % 2L == 1L);
			Assert.Contains(numbers, n => n % 2L == 0L);
		}

		[Fact]
		public void ServiceDaysLabel_Laborables_OnSheetHeader()
		{
			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			SfmDemoInfrastructure.Apply(topo);
			Plan plan = new Plan(topo);
			plan.EnsureDefaultTrainSpecs();
			Assert.True(plan.CompileDemand("""
				require both ways every 60 min PMI -> MAN 06:00-10:00 as R-T3
				  days lab
				  stops 30s
				""").Success);
			Mesh mesh = new MeshPlanner(plan).Solve(DayOfWeek.Monday);
			Circulation c = mesh.Circulations[0];
			ServiceDays? days = ItineraryBookDocument.ResolveServiceDays(c, plan, null, mesh.PlanningDay);
			Assert.NotNull(days);
			Assert.Equal("Laborables", days!.FormatCirculationLabel());

			CirculationSheetDocument doc = CirculationSheetDocument.Build(c, mesh, 30, null, days);
			Assert.Equal("Laborables", doc.ServiceDaysLabel);
			string svg = CirculationSheetSvgRenderer.RenderAllPages(doc)[0];
			Assert.Contains("Laborables", svg, StringComparison.Ordinal);
		}

		[Fact]
		public void StandaloneSheet_Unofficial_HasNoSealOrQr()
		{
			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			SfmDemoInfrastructure.Apply(topo);
			Plan plan = new Plan(topo);
			plan.EnsureDefaultTrainSpecs();
			Assert.True(plan.CompileDemand("""
				require both ways every 60 min PMI -> MAN 06:00-10:00 as R-T3
				  days lab
				  stops 30s
				""").Success);
			Mesh mesh = new MeshPlanner(plan).Solve(DayOfWeek.Monday);
			CirculationSheetDocument doc = CirculationSheetDocument.Build(mesh.Circulations[0], mesh, 30);
			string unofficial = CirculationSheetSvgRenderer.RenderAllPages(
				doc, out string seal, out string payload, official: false)[0];
			Assert.Equal(string.Empty, seal);
			Assert.Equal(string.Empty, payload);
			Assert.DoesNotContain("SEL ", unofficial, StringComparison.Ordinal);
			Assert.DoesNotContain("ZAFSEL:", unofficial, StringComparison.Ordinal);
			Assert.DoesNotContain("diamond-circ-qr", unofficial, StringComparison.Ordinal);

			string official = CirculationSheetSvgRenderer.RenderAllPages(
				doc, out string officialSeal, out _, official: true)[0];
			Assert.False(string.IsNullOrEmpty(officialSeal));
			Assert.Contains("SEL " + officialSeal, official, StringComparison.Ordinal);
			Assert.Contains("diamond-circ-qr", official, StringComparison.Ordinal);
		}

		[Fact]
		public void CompleteBook_AddsTemporaryLimits_YellowAndReason()
		{
			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			SfmDemoInfrastructure.Apply(topo);
			Axis? t3 = topo.FindAxisById("T3");
			Assert.NotNull(t3);
			TemporarySpeedLimit temp = TopoTemporaryLimits.FromSpan(
				"T3", 8000L, 12000L, 30, TemporaryLimitReason.Works, "Obra de vía");
			TopoTemporaryLimits.Apply(topo, new[] { temp });

			Plan plan = new Plan(topo);
			plan.EnsureDefaultTrainSpecs();
			Assert.True(plan.CompileDemand("""
				require both ways every 60 min PMI -> MAN 06:00-10:00 as R-T3
				  days lab
				  stops 30s
				""").Success);
			Mesh mesh = new MeshPlanner(plan).Solve(DayOfWeek.Monday);
			Assert.NotEmpty(mesh.Circulations);
			Circulation c = mesh.Circulations[0];

			CirculationSheetDocument normal = CirculationSheetDocument.Build(
				c, mesh, 30, includeTemporaryLimits: false);
			CirculationSheetDocument complete = CirculationSheetDocument.Build(
				c, mesh, 30, includeTemporaryLimits: true);

			Assert.True(complete.Frontiers.Count > normal.Frontiers.Count);
			Assert.DoesNotContain(normal.Frontiers, f => f.OutgoingIsTemporary);
			Assert.Contains(complete.Frontiers, f => f.OutgoingIsTemporary && f.TemporaryReasonLabel == "Obras");

			string svg = CirculationSheetSvgRenderer.RenderAllPages(complete)[0];
			Assert.Contains("#ffd400", svg, StringComparison.OrdinalIgnoreCase);
			Assert.Contains("Obras", svg, StringComparison.Ordinal);
			Assert.Contains("Obra de vía", svg, StringComparison.Ordinal);

			string svgNormal = CirculationSheetSvgRenderer.RenderAllPages(normal)[0];
			Assert.DoesNotContain("#ffd400", svgNormal, StringComparison.OrdinalIgnoreCase);
		}

		[Fact]
		public void FormatCirculationLabel_Rules()
		{
			Assert.Equal("Laborables", ServiceDays.Laborables.FormatCirculationLabel());
			Assert.Equal("Fines de semana", ServiceDays.Festivos.FormatCirculationLabel());
			Assert.Equal("Diario", ServiceDays.All.FormatCirculationLabel());
			Assert.Equal("lunes", new ServiceDays(ServiceDay.Monday).FormatCirculationLabel());
			Assert.Equal("L, X, V", new ServiceDays(ServiceDay.Monday | ServiceDay.Wednesday | ServiceDay.Friday).FormatCirculationLabel());
		}

		[Fact]
		public void ExportPdf_ProducesNonEmptyPdf()
		{
			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			SfmDemoInfrastructure.Apply(topo);
			Plan plan = new Plan(topo);
			plan.EnsureDefaultTrainSpecs();
			Assert.True(plan.CompileDemand("""
				require both ways every 60 min PMI -> MAN 06:00-08:00 as R
				  days lab
				  stops 30s
				""").Success);
			Mesh mesh = new MeshPlanner(plan).Solve(DayOfWeek.Monday);
			CirculationSheetDocument doc = CirculationSheetDocument.Build(mesh.Circulations[0], mesh, 30);
			IReadOnlyList<string> sheets = CirculationSheetSvgRenderer.RenderAllPages(doc);
			byte[] pdf = CirculationSheetPdfExporter.ExportSvgSheetsToPdf(sheets);
			Assert.True(pdf.Length > 100);
			// Cabecera PDF
			string head = System.Text.Encoding.ASCII.GetString(pdf, 0, Math.Min(8, pdf.Length));
			Assert.StartsWith("%PDF", head, StringComparison.Ordinal);
		}

		[Fact]
		public void SingleTrain_StillRendersIndependently()
		{
			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			SfmDemoInfrastructure.Apply(topo);
			Plan plan = new Plan(topo);
			plan.EnsureDefaultTrainSpecs();
			Assert.True(plan.CompileDemand("""
				require both ways every 60 min PMI -> MAN 06:00-08:00 as R
				  days lab
				  stops 30s
				""").Success);
			Mesh mesh = new MeshPlanner(plan).Solve(DayOfWeek.Monday);
			Circulation c = mesh.Circulations[0];
			CirculationSheetDocument doc = CirculationSheetDocument.Build(c, mesh, 30);
			IReadOnlyList<string> sheets = CirculationSheetSvgRenderer.RenderAllPages(doc);
			Assert.NotEmpty(sheets);
			Assert.Contains("viewBox=\"0 0 841.89 595.28\"", sheets[0], StringComparison.Ordinal);
		}

		[Fact]
		public void RouteHeader_IsTwoLines_TitleThenViewPk()
		{
			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			SfmDemoInfrastructure.Apply(topo);
			Plan plan = new Plan(topo);
			plan.EnsureDefaultTrainSpecs();
			Assert.True(plan.CompileDemand("""
				require both ways every 60 min PMI -> MAN 06:00-08:00 as R
				  days lab
				  stops 30s
				""").Success);
			Mesh mesh = new MeshPlanner(plan).Solve(DayOfWeek.Monday);
			CirculationSheetDocument doc = CirculationSheetDocument.Build(mesh.Circulations[0], mesh, 30);
			Assert.Contains(" - ", doc.RouteTitle, StringComparison.Ordinal);
			Assert.False(string.IsNullOrWhiteSpace(doc.RouteLine));
			Assert.DoesNotContain(".- ", doc.RouteTitle, StringComparison.Ordinal);
			string svg = CirculationSheetSvgRenderer.RenderAllPages(doc)[0];
			Assert.Contains(doc.RouteTitle, svg, StringComparison.Ordinal);
			Assert.Contains(doc.RouteLine, svg, StringComparison.Ordinal);
			Assert.Contains("diamond-logo-gray", svg, StringComparison.Ordinal);
			Assert.Contains("diamond-doc-logo-gray", svg, StringComparison.Ordinal);
		}
	}
}
