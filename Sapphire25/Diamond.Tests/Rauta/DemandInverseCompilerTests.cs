using Diamond.Rauta;
using Diamond.Timed;
using Diamond.Topo;

namespace Diamond.Tests.Rauta
{
	public class DemandInverseCompilerTests
	{
		private static string RautaPath =>
			Path.Combine(AppContext.BaseDirectory, "Samples", "Onice", "rautasfm227.xml");

		private static string TopoPath => SamplePaths.TopoSfm227;

		[Fact]
		public void Load_Rauta_HasInv2026PlanWithBlocks()
		{
			Assert.True(File.Exists(RautaPath), "rautasfm227.xml debe copiarse al output de tests");
			RautaDocument doc = RautaXmlSerializer.Load(RautaPath);
			Assert.Equal("Invierno 2025 2026", doc.Info.Name);
			Assert.NotEmpty(doc.Info.TopoId);
			RautaPlan? plan = doc.FindPlanById("Inv2026");
			Assert.NotNull(plan);
			Assert.True(plan!.Blocks.Count >= 10);
			Assert.Contains(plan.Blocks, b => b.AsimilationId == "44x3L");
		}

		[Fact]
		public void InverseCompile_ProducesParsableDemandScript()
		{
			RautaDocument doc = RautaXmlSerializer.Load(RautaPath);
			TopoAsimilationCatalog asims = TopoAsimilationCatalog.LoadFromTopoXml(TopoPath);
			TopoLayout layout = TopoXmlSerializer.Load(TopoPath);

			RautaPlan plan = doc.FindPlanById("Inv2026")!;
			DemandInverseCompiler.InverseCompileResult inv =
				DemandInverseCompiler.Compile(plan, asims, layout);

			Assert.True(inv.RequirementCount > 0, "debe inferir al menos un requisito");
			Assert.Contains("require", inv.Script, StringComparison.Ordinal);
			Assert.Contains("days lab", inv.Script, StringComparison.Ordinal);
			Assert.Contains("PMI", inv.Script, StringComparison.Ordinal);
			Assert.Contains("MAN", inv.Script, StringComparison.Ordinal);

			// El script generado debe compilar en el parser de demanda
			DemandCompileResult parsed = DemandScriptParser.Parse(inv.Script);
			Assert.True(parsed.Success, string.Join("; ", parsed.Errors));
			Assert.Equal(inv.RequirementCount, parsed.Requirements.Count);

			// Y resolverse contra el topo
			Plan diamondPlan = new Plan(layout);
			DemandCompileResult resolved = diamondPlan.CompileDemand(inv.Script);
			Assert.True(resolved.Success, string.Join("; ", resolved.Errors));
		}

		[Fact]
		public void InverseCompile_IsDeterministic()
		{
			RautaDocument doc = RautaXmlSerializer.Load(RautaPath);
			TopoAsimilationCatalog asims = TopoAsimilationCatalog.LoadFromTopoXml(TopoPath);
			TopoLayout layout = TopoXmlSerializer.Load(TopoPath);
			RautaPlan plan = doc.FindPlanById("Inv2026")!;

			string a = DemandInverseCompiler.Compile(plan, asims, layout).Script;
			string b = DemandInverseCompiler.Compile(plan, asims, layout).Script;
			Assert.Equal(a, b);
		}

		[Fact]
		public void InverseCompile_IncaSaPobla_70x_IsNotPalmaAndNotDenseCadence()
		{
			RautaDocument doc = RautaXmlSerializer.Load(RautaPath);
			TopoAsimilationCatalog asims = TopoAsimilationCatalog.LoadFromTopoXml(TopoPath);
			TopoLayout layout = TopoXmlSerializer.Load(TopoPath);
			SfmDemoInfrastructure.Apply(layout);
			RautaPlan plan = doc.FindPlanById("Inv2026")!;

			DemandInverseCompiler.InverseCompileResult inv =
				DemandInverseCompiler.Compile(plan, asims, layout);

			// OD correcto Inca–Sa Pobla (no Palma)
			Assert.True(
				inv.Script.Contains("INC -> SPB", StringComparison.Ordinal)
				|| inv.Script.Contains("SPB -> INC", StringComparison.Ordinal),
				"Debe existir INC↔SPB");
			Assert.Contains("70x", inv.Script, StringComparison.Ordinal);

			// No both-ways denso todo el día para n=1+1
			Assert.DoesNotContain(
				"both ways every 60 min INC -> SPB",
				inv.Script,
				StringComparison.Ordinal);
			Assert.DoesNotContain(
				"both ways every 60 min SPB -> INC",
				inv.Script,
				StringComparison.Ordinal);

			Plan diamondPlan = new Plan(layout);
			diamondPlan.EnsureDefaultTrainSpecs();
			DemandCompileResult resolved = diamondPlan.CompileDemand(inv.Script);
			Assert.True(resolved.Success, string.Join("; ", resolved.Errors));

			Mesh mesh = new MeshPlanner(diamondPlan).Solve(DayOfWeek.Monday);
			List<Circulation> series70 = mesh.Circulations
				.Where(c => c.HasServiceNumber
					&& int.TryParse(c.ServiceNumber, out int n)
					&& n >= 7000 && n < 7100)
				.OrderBy(c => c.ServiceNumber, StringComparer.Ordinal)
				.ToList();

			// Solo un puñado (≈ 7001 ida + 7002 vuelta), no una malla cada hora
			Assert.InRange(series70.Count, 1, 4);
			Assert.All(series70, c =>
			{
				string o = c.Asimilation.Origin.Station.Avr;
				string d = c.Asimilation.Destination.Station.Avr;
				bool ok = (o == "INC" && d == "SPB") || (o == "SPB" && d == "INC");
				Assert.True(ok, "70xx debe ser INC↔SPB, no PMI: " + o + "->" + d);
			});

			// 70xx (INC↔SPB) puede proyectarse en T3+T2 solo en el tramo T2 (Enllaç–SPB),
			// no es el mismo corredor completo que PMI↔SPB.
			RouteView? palmaSpb = null;
			Station? pmi = layout.Stations.FirstOrDefault(s => s.Avr == "PMI" || s.Id == "01");
			Station? spb = layout.Stations.FirstOrDefault(s => s.Avr == "SPB" || s.Id == "33");
			if (pmi is not null && spb is not null)
			{
				StationOnRoute? o;
				StationOnRoute? dest;
				RouteView.TryFindPath(layout, pmi, spb, out palmaSpb, out o, out dest);
			}

			if (palmaSpb is not null)
			{
				Assert.All(series70, c =>
					Assert.False(
						c.Asimilation.View.IsSameOrReversePath(palmaSpb),
						"700x no es el corredor completo Palma–SPB"));
			}
		}

		[Fact]
		public void InverseCompile_IncludesPalmaSaPobla_MultiAxis()
		{
			RautaDocument doc = RautaXmlSerializer.Load(RautaPath);
			TopoAsimilationCatalog asims = TopoAsimilationCatalog.LoadFromTopoXml(TopoPath);
			TopoLayout layout = TopoXmlSerializer.Load(TopoPath);
			SfmDemoInfrastructure.Apply(layout);
			RautaPlan plan = doc.FindPlanById("Inv2026")!;

			DemandInverseCompiler.InverseCompileResult inv =
				DemandInverseCompiler.Compile(plan, asims, layout);

			Assert.Contains("PMI -> SPB", inv.Script, StringComparison.Ordinal);
			Assert.Contains("days lab", inv.Script, StringComparison.Ordinal);
			Assert.Contains("days fes", inv.Script, StringComparison.Ordinal);
			Assert.Contains("multi-eje", inv.Script, StringComparison.OrdinalIgnoreCase);
			Assert.Contains("T3:", inv.Script, StringComparison.Ordinal);
			Assert.Contains("T2:", inv.Script, StringComparison.Ordinal);

			// No debe confundir Inca con Pont d'Inca (pdi); OD Inca–Sa Pobla multi-eje
			Assert.DoesNotContain("SPB -> pdi", inv.Script, StringComparison.Ordinal);
			Assert.DoesNotContain("pdi -> SPB", inv.Script, StringComparison.Ordinal);
			Assert.True(
				inv.Script.Contains("INC -> SPB", StringComparison.Ordinal)
				|| inv.Script.Contains("SPB -> INC", StringComparison.Ordinal),
				"Debe existir requisito INC↔SPB (no pdi).");

			// Destino no va en skip; Son Rullán se emite por AVR
			Assert.DoesNotContain("skip \"sa pobla\"", inv.Script, StringComparison.OrdinalIgnoreCase);
			Assert.Contains("skip RLL", inv.Script, StringComparison.OrdinalIgnoreCase);
			Assert.Contains("dwell INC", inv.Script, StringComparison.OrdinalIgnoreCase);

			// Compila y resuelve
			Plan diamondPlan = new Plan(layout);
			diamondPlan.EnsureDefaultTrainSpecs();
			DemandCompileResult resolved = diamondPlan.CompileDemand(inv.Script);
			Assert.True(resolved.Success, string.Join("; ", resolved.Errors));

			DemandRequirement? spbLab = null;
			int i = 0;
			while (i < resolved.Requirements.Count)
			{
				DemandRequirement req = resolved.Requirements[i];
				if (string.Equals(req.From.Text, "PMI", StringComparison.OrdinalIgnoreCase)
					&& string.Equals(req.To.Text, "SPB", StringComparison.OrdinalIgnoreCase)
					&& req.ServiceDays.AppliesOn(DayOfWeek.Monday))
				{
					spbLab = req;
					break;
				}

				i++;
			}

			Assert.NotNull(spbLab);
			Assert.NotNull(spbLab!.FromStation);
			Assert.NotNull(spbLab.ToStation);

			// El planificador encuentra camino multi-eje
			RouteView? view;
			StationOnRoute? origin;
			StationOnRoute? destination;
			bool ok = RouteView.TryFindPath(
				layout,
				spbLab.FromStation!,
				spbLab.ToStation!,
				out view,
				out origin,
				out destination);
			Assert.True(ok);
			Assert.NotNull(view);
			Assert.True(view!.Legs.Count >= 2);
		}
	}
}
