using Diamond.Rauta;
using Diamond.Timed;
using Diamond.Topo;

namespace Diamond.Controls.Services
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
			include toposfm227
			plan "SFM T3 laborables"
			notes "Malla de pruebas del programa Diamond"
			train default "SFM diésel" accel 0.9 brake 0.8 vmax 100
			# Cadencia ~40 min; ida y vuelta; cruce en Petra
			require both ways every 40 min PMI -> MAN 06:00-22:00 using default as R-T3
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
			include toposfm227
			plan "SFM Palma-Sa Pobla"
			notes "Corredor multi-eje Palma – Sa Pobla"
			train default "SFM diésel" accel 0.9 brake 0.8 vmax 100
			require both ways every 60 min PMI -> SPB 06:00-21:00 using default as R-SPB
			  days lab
			  stops 30s
			  dwell INC 1min
			""";

		public const string DefaultTopoFileName = "toposfm227.xml";
		public const string DefaultRautaFileName = "rautasfm227.xml";

		/// <summary>Nombre lógico del include demo (<c>include toposfm227</c>).</summary>
		public const string DefaultTopoIncludeName = "toposfm227";

		public static string ResolveTopoPath()
		{
			return ResolveSamplePath(DefaultTopoFileName);
		}

		public static string ResolveRautaPath()
		{
			return ResolveSamplePath(DefaultRautaFileName);
		}

		/// <summary>
		/// Intenta resolver un sample en disco. En WASM suele fallar: use
		/// <see cref="RegisterTopoXml"/> con contenido HTTP o InputFile.
		/// </summary>
		public static bool TryResolveSamplePath(string fileName, out string? path)
		{
			path = null;
			string[] candidates = BuildSampleCandidates(fileName);
			int index = 0;
			while (index < candidates.Length)
			{
				try
				{
					if (File.Exists(candidates[index]))
					{
						path = candidates[index];
						return true;
					}
				}
				catch
				{
					// Path.GetFullPath / File.Exists pueden fallar en browser.
				}

				index++;
			}

			return false;
		}

		private static string ResolveSamplePath(string fileName)
		{
			if (TryResolveSamplePath(fileName, out string? path) && path is not null)
			{
				return path;
			}

			throw new FileNotFoundException(
				"No se encontró " + fileName
				+ ". En el navegador no hay acceso a disco: cargue el XML (Topo…) o precargue Samples/Onice en wwwroot vía HTTP.");
		}

		private static string[] BuildSampleCandidates(string fileName)
		{
			List<string> list = new List<string>
			{
				Path.Combine(AppContext.BaseDirectory, "Samples", "Onice", fileName),
				Path.Combine(AppContext.BaseDirectory, "wwwroot", "Samples", "Onice", fileName)
			};

			try
			{
				list.Add(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Samples", "Onice", fileName)));
				list.Add(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Diamond.Controls", "Samples", "Onice", fileName)));
				list.Add(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Diamond.Tests", "Samples", "Onice", fileName)));
				list.Add(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Sapphire25", "Diamond.Controls", "Samples", "Onice", fileName)));
			}
			catch
			{
				// Ignore path resolution failures on constrained hosts (WASM).
			}

			return list.ToArray();
		}

		/// <summary>
		/// Registra un XML de topología en memoria (WASM / host). Hace disponible
		/// <c>include toposfm227</c> y <see cref="LoadTopoWithInfrastructure"/>.
		/// </summary>
		public static TopoLayout RegisterTopoXml(string logicalName, string xmlText)
		{
			TopoStorage storage = TopoStorage.LoadFromXmlText(logicalName, xmlText);
			SfmDemoInfrastructure.Apply(storage.Layout);
			// Re-registrar tras Apply (misma instancia mutada).
			TopoStorage.RegisterInMemory(logicalName, storage.Layout, xmlText);
			TopoStorage.RegisterInMemory(DefaultTopoIncludeName, storage.Layout, xmlText);
			TopoStorage.RegisterInMemory(DefaultTopoFileName, storage.Layout, xmlText);
			return storage.Layout;
		}

		/// <summary>True si hay topología demo usable (disco o memoria).</summary>
		public static bool HasUsableTopo()
		{
			if (TopoStorage.HasMemoryCatalog)
			{
				return true;
			}

			return TryResolveSamplePath(DefaultTopoFileName, out _);
		}

		/// <summary>
		/// Directorio base para <see cref="Plan.ScriptBaseDirectory"/>: carpeta del sample en disco,
		/// o vacío si solo hay memoria (el include usa el catálogo en memoria).
		/// </summary>
		public static string GetScriptBaseDirectory()
		{
			if (TryResolveSamplePath(DefaultTopoFileName, out string? path) && path is not null)
			{
				return Path.GetDirectoryName(path) ?? string.Empty;
			}

			return string.Empty;
		}

		public static TopoLayout LoadTopoWithInfrastructure()
		{
			// Preferir catálogo en memoria (WASM).
			if (TopoStorage.TryLoadFromXml(DefaultTopoIncludeName, null, out TopoStorage? mem, out _)
				&& mem is not null)
			{
				SfmDemoInfrastructure.Apply(mem.Layout);
				return mem.Layout;
			}

			TopoLayout topo = TopoXmlSerializer.Load(ResolveTopoPath());
			SfmDemoInfrastructure.Apply(topo);
			TopoStorage.RegisterInMemory(DefaultTopoIncludeName, topo);
			TopoStorage.RegisterInMemory(DefaultTopoFileName, topo);
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
			// La topología se declara en el script (include) y se carga al compilar.
			Plan plan = new Plan();
			plan.Name = "SFM demo";
			plan.ScriptBaseDirectory = System.IO.Path.GetDirectoryName(ResolveTopoPath()) ?? string.Empty;
			plan.EnsureDefaultTrainSpecs();
			plan.DemandScript = demandScript ?? string.Empty;

			DemandCompileResult compiled = plan.CompileDemand();
			if (!compiled.Success)
			{
				throw new InvalidOperationException(
					"Falló la compilación de demanda: " + string.Join("; ", compiled.Errors));
			}

			if (plan.Topo is null)
			{
				throw new InvalidOperationException(
					"El script no cargó topología. Añada include toposfm227 o asigne Plan.Topo.");
			}

			// Capa de infraestructura demo (vías/cantones SFM) sobre la topo del include.
			SfmDemoInfrastructure.Apply(plan.Topo);

			TopoLayout topo = plan.Topo;
			IReadOnlyList<RouteView> views = BuildDemoViews(topo);

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

