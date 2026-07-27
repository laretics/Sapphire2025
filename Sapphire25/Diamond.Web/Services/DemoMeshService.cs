using Diamond.Rauta;
using Diamond.Timed;
using Diamond.Topo;

namespace Diamond.Web.Services
{
	/// <summary>
	/// Escenarios de visualización SFM: topología, vistas de ruta y demanda manual o inferida desde rauta.
	/// </summary>
	public static class DemoMeshService
	{
		public const string DefaultAxisId = "T3";
		public const string DefaultViewId = "T3";
		public const string PalmaSaPoblaViewId = "T3+T2";
		public const string T2ViewId = "T2";

		/// <summary>
		/// Demanda manual de referencia (laborables T3).
		/// </summary>
		public const string SfmT3DemandScript = """
			plan "SFM T3 laborables"
			# Cadencia ~40 min; ida y vuelta; cruce en Petra
			require both ways every 40 min PMI -> MAN 06:00-22:00 as R-T3
			  days lab
			  stops 30s
			  skip RLL Enllaç "Sant Joan" PSJ
			  dwell INC 1min
			  cross at Petra
			""";

		/// <summary>
		/// Demanda multi-eje Palma–Sa Pobla (T3 + T2).
		/// </summary>
		public const string SfmPalmaSaPoblaDemandScript = """
			plan "SFM Palma-Sa Pobla"
			require both ways every 60 min PMI -> SPB 06:00-21:00 as R-SPB
			  days lab
			  stops 30s
			  dwell INC 1min
			""";

		public static string ResolveTopoPath()
		{
			return ResolveSamplePath("toposfm227.xml");
		}

		public static string ResolveRautaPath()
		{
			return ResolveSamplePath("rautasfm227.xml");
		}

		private static string ResolveSamplePath(string fileName)
		{
			string[] candidates = new[]
			{
				Path.Combine(AppContext.BaseDirectory, "Samples", "Onice", fileName),
				Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Samples", "Onice", fileName)),
				Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Diamond.Tests", "Samples", "Onice", fileName))
			};

			int index = 0;
			while (index < candidates.Length)
			{
				if (File.Exists(candidates[index]))
				{
					return candidates[index];
				}

				index++;
			}

			throw new FileNotFoundException(
				"No se encontró " + fileName + ". Compila Diamond.Web para copiar Samples/ al output.");
		}

		public static TopoLayout LoadTopoWithInfrastructure()
		{
			TopoLayout topo = TopoXmlSerializer.Load(ResolveTopoPath());
			SfmDemoInfrastructure.Apply(topo);
			return topo;
		}

		public static Axis RequireAxis(TopoLayout topo, string axisId)
		{
			Axis? axis = topo.FindAxisById(axisId);
			if (axis is null)
			{
				throw new InvalidOperationException("No se encontró el eje '" + axisId + "' en el sample.");
			}

			return axis;
		}

		/// <summary>
		/// Catálogo de vistas de malla disponibles para el render (selector UI).
		/// </summary>
		public static IReadOnlyList<RouteView> BuildDemoViews(TopoLayout topo)
		{
			if (topo is null)
			{
				throw new ArgumentNullException(nameof(topo));
			}

			List<RouteView> views = new List<RouteView>();

			Axis? t3 = topo.FindAxisById("T3");
			Axis? t2 = topo.FindAxisById("T2");
			Axis? m1 = topo.FindAxisById("M1");

			if (t3 is not null)
			{
				views.Add(RouteView.FromAxis(t3));
			}

			if (t2 is not null)
			{
				views.Add(RouteView.FromAxis(t2));
			}

			if (m1 is not null)
			{
				views.Add(RouteView.FromAxis(m1));
			}

			// Vista multi-eje: Palma (T3) → Enllaç → Sa Pobla (T2).
			if (t3 is not null && t2 is not null)
			{
				StationOnAxis? palma = FindPlacementByAvrOrId(t3, "PMI", "01");
				StationOnAxis? enllacT3 = FindPlacementByNameContains(t3, "Enlla");
				StationOnAxis? enllacT2 = FindPlacementByNameContains(t2, "Enlla");
				StationOnAxis? spb = FindPlacementByAvrOrId(t2, "SPB", "33");

				if (palma is not null && enllacT3 is not null && enllacT2 is not null && spb is not null)
				{
					List<(Axis Axis, long FromPk, long ToPk)> segs = new List<(Axis, long, long)>();
					segs.Add((t3, palma.PK, enllacT3.PK));
					segs.Add((t2, enllacT2.PK, spb.PK));
					views.Add(RouteView.Concat(PalmaSaPoblaViewId, "Palma → Sa Pobla", segs));
				}
			}

			return views;
		}

		public static RouteView? FindViewById(IReadOnlyList<RouteView> views, string viewId)
		{
			if (views is null || string.IsNullOrEmpty(viewId))
			{
				return null;
			}

			int index = 0;
			while (index < views.Count)
			{
				if (string.Equals(views[index].Id, viewId, StringComparison.OrdinalIgnoreCase))
				{
					return views[index];
				}

				index++;
			}

			return null;
		}

		/// <summary>
		/// Compilador inverso: rauta + topo → script de demanda (borrador).
		/// </summary>
		public static DemandInverseCompiler.InverseCompileResult ImportRautaToScript()
		{
			RautaDocument rauta = RautaXmlSerializer.Load(ResolveRautaPath());
			TopoAsimilationCatalog asims = TopoAsimilationCatalog.LoadFromTopoXml(ResolveTopoPath());
			TopoLayout layout = LoadTopoWithInfrastructure();

			RautaPlan? plan = rauta.FindPlanById("Inv2026");
			if (plan is null && rauta.Plans.Count > 0)
			{
				plan = rauta.Plans[0];
			}

			if (plan is null)
			{
				throw new InvalidOperationException("El fichero rauta no contiene ningún plan.");
			}

			return DemandInverseCompiler.Compile(plan, asims, layout);
		}

		public static (Plan Plan, Mesh Mesh, RouteView View, IReadOnlyList<RouteView> Views) BuildDemo()
		{
			return BuildWithScript(SfmT3DemandScript, DayOfWeek.Monday, DefaultViewId);
		}

		public static (Plan Plan, Mesh Mesh, RouteView View, IReadOnlyList<RouteView> Views) BuildWithScript(
			string demandScript,
			DayOfWeek dayOfWeek,
			string preferredViewId = DefaultViewId)
		{
			TopoLayout topo = LoadTopoWithInfrastructure();
			IReadOnlyList<RouteView> views = BuildDemoViews(topo);

			Plan plan = new Plan(topo);
			plan.Name = "SFM demo";
			plan.EnsureDefaultTrainSpecs();
			plan.DemandScript = demandScript ?? string.Empty;

			DemandCompileResult compiled = plan.CompileDemand();
			if (!compiled.Success)
			{
				throw new InvalidOperationException(
					"Falló la compilación de demanda: " + string.Join("; ", compiled.Errors));
			}

			Mesh mesh = new MeshPlanner(plan).Solve(dayOfWeek);

			RouteView? view = FindViewById(views, preferredViewId);
			if (view is null && views.Count > 0)
			{
				view = views[0];
			}

			if (view is null)
			{
				throw new InvalidOperationException("No hay vistas de ruta disponibles en la topología.");
			}

			// Si la malla tiene asimilaciones, preferir la vista del primer camino planificado.
			if (mesh.Asimilations.Count > 0)
			{
				RouteView planned = mesh.Asimilations[0].View;
				RouteView? match = FindViewById(views, planned.Id);
				if (match is null)
				{
					// Añadir la vista planificada al catálogo si no estaba (p.ej. camino inventado por BFS).
					List<RouteView> extended = new List<RouteView>(views);
					extended.Add(planned);
					views = extended;
					view = planned;
				}
				else if (string.Equals(preferredViewId, DefaultViewId, StringComparison.Ordinal)
					|| FindViewById(views, preferredViewId) is null)
				{
					view = match;
				}
			}

			return (plan, mesh, view, views);
		}

		/// <summary>
		/// Replanifica un plan ya cargado para otro día (sin recompilar el script).
		/// </summary>
		public static Mesh SolveForDay(Plan plan, DayOfWeek dayOfWeek)
		{
			if (plan is null)
			{
				throw new ArgumentNullException(nameof(plan));
			}

			return new MeshPlanner(plan).Solve(dayOfWeek);
		}

		private static StationOnAxis? FindPlacementByAvrOrId(Axis axis, string avr, string id)
		{
			int index = 0;
			while (index < axis.Stations.Count)
			{
				StationOnAxis p = axis.Stations[index];
				if (string.Equals(p.Station.Id, id, StringComparison.Ordinal)
					|| string.Equals(p.Station.Avr, avr, StringComparison.OrdinalIgnoreCase))
				{
					return p;
				}

				index++;
			}

			return null;
		}

		private static StationOnAxis? FindPlacementByNameContains(Axis axis, string nameFragment)
		{
			int index = 0;
			while (index < axis.Stations.Count)
			{
				StationOnAxis p = axis.Stations[index];
				string name = p.Station.Name ?? string.Empty;
				if (name.IndexOf(nameFragment, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return p;
				}

				index++;
			}

			return null;
		}
	}
}
