using Diamond.Timed;
using Diamond.Topo;

namespace Diamond.Web.Services
{
	/// <summary>
	/// Escenario de visualización: eje T3 (Palma–Manacor) del sample SFM,
	/// con cadencia y patrón de paradas realistas.
	/// </summary>
	public static class DemoMeshService
	{
		public const string DefaultAxisId = "T3";

		/// <summary>
		/// Demanda actual SFM Palma–Manacor: cada 40 min, paradas 30 s salvo skips,
		/// 1 min en Inca, cruce en Enllaç.
		/// </summary>
		public const string SfmT3DemandScript = """
			plan "SFM T3 laborables"
			# Cadencia ~40 min; ida y vuelta; cruce en Petra (no viable en Enllaç a 40 min)
			require both ways every 40 min PMI -> MAN 06:00-22:00 as R-T3
			  stops 30s
			  skip RLL Enllaç "Sant Joan" PSJ
			  dwell INC 1min
			  cross at Petra
			""";

		public static string ResolveTopoPath()
		{
			string[] candidates = new[]
			{
				Path.Combine(AppContext.BaseDirectory, "Samples", "Onice", "toposfm227.xml"),
				Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Samples", "Onice", "toposfm227.xml")),
				Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Diamond.Tests", "Samples", "Onice", "toposfm227.xml"))
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
				"No se encontró toposfm227.xml. Compila Diamond.Web para copiar Samples/ al output.");
		}

		public static (Plan Plan, Mesh Mesh, Axis Axis) BuildDemo()
		{
			return BuildFromSample(DefaultAxisId);
		}

		public static (Plan Plan, Mesh Mesh, Axis Axis) BuildFromSample(string axisId)
		{
			string path = ResolveTopoPath();
			TopoLayout topo = TopoXmlSerializer.Load(path);
			SfmDemoInfrastructure.Apply(topo);

			Axis? axis = topo.FindAxisById(axisId);
			if (axis is null)
			{
				throw new InvalidOperationException("No se encontró el eje '" + axisId + "' en el sample.");
			}

			Plan plan = new Plan(topo);
			plan.Name = "SFM — " + axis.Name;
			plan.EnsureDefaultTrainSpecs();
			plan.DemandScript = SfmT3DemandScript;

			DemandCompileResult compiled = plan.CompileDemand();
			if (!compiled.Success)
			{
				throw new InvalidOperationException(
					"Falló la compilación de demanda: " + string.Join("; ", compiled.Errors));
			}

			Mesh mesh = new MeshPlanner(plan).Solve();
			return (plan, mesh, axis);
		}
	}
}
