using Diamond.Timed;
using Diamond.Topo;

namespace Diamond.Tests.Timed
{
	public class TopoIncludeResolverTests
	{
		public TopoIncludeResolverTests()
		{
			// Aislar tests del catálogo estático de sesión / host.
			TopoStorage.ClearMemoryCatalog();
			TopoStorage.SetDefaultIncludeResolver(null);
		}

		[Fact]
		public void DictionaryResolver_ResolvesByBareNameAndXml()
		{
			TopoLayout layout = new TopoLayout();
			layout.Info.Name = "demo";
			TopoStorage storage = new TopoStorage("toposfm227.xml", "zafiro:demo", layout);

			DictionaryTopoIncludeResolver resolver = new DictionaryTopoIncludeResolver();
			resolver.Add(storage, "toposfm227", "SFM");

			TopoStorage? found;
			string? error;
			Assert.True(resolver.TryResolve("toposfm227", out found, out error));
			Assert.NotNull(found);
			Assert.Same(layout, found!.Layout);

			Assert.True(resolver.TryResolve("toposfm227.xml", out found, out error));
			Assert.NotNull(found);

			Assert.True(resolver.TryResolve("SFM", out found, out error));
			Assert.NotNull(found);
		}

		[Fact]
		public void TryLoadFromXml_UsesResolverBeforeDisk()
		{
			TopoLayout layout = new TopoLayout();
			TopoStorage storage = new TopoStorage("virtual.xml", "zafiro:v", layout);
			DictionaryTopoIncludeResolver resolver = new DictionaryTopoIncludeResolver();
			resolver.Add(storage, "virtual");

			TopoStorage? loaded;
			string? error;
			bool ok = TopoStorage.TryLoadFromXml(
				"virtual",
				baseDirectory: @"C:\ruta\que\no\existe",
				includeResolver: resolver,
				out loaded,
				out error);

			Assert.True(ok, error);
			Assert.NotNull(loaded);
			Assert.Same(layout, loaded!.Layout);
		}

		[Fact]
		public void TryLoadFromXml_NullResolver_FallsBackToDiskSample()
		{
			string dir = Path.GetDirectoryName(SamplePaths.TopoSfm227)!;
			TopoStorage? loaded;
			string? error;
			bool ok = TopoStorage.TryLoadFromXml(
				"toposfm227.xml",
				dir,
				includeResolver: null,
				out loaded,
				out error);

			Assert.True(ok, error);
			Assert.NotNull(loaded);
			Assert.True(File.Exists(loaded!.ResolvedPath));
		}

		[Fact]
		public void Plan_CompileDemand_Include_UsesPlanResolver()
		{
			TopoLayout layout = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			TopoStorage storage = new TopoStorage("red-sfm.xml", "zafiro:red", layout);
			DictionaryTopoIncludeResolver resolver = new DictionaryTopoIncludeResolver();
			resolver.Add(storage, "red-sfm", "alias-sfm");

			Plan plan = new Plan();
			plan.IncludeResolver = resolver;
			plan.EnsureDefaultTrainSpecs();
			// Sin baseDirectory de disco: debe resolver solo por almacén.
			plan.ScriptBaseDirectory = string.Empty;

			string script = """
				include red-sfm
				plan "from store"
				require 1/h PMI -> MAN 06:00-07:00 as R1
				""";

			DemandCompileResult result = plan.CompileDemand(script);
			Assert.True(result.Success, string.Join("; ", result.Errors));
			Assert.NotNull(plan.Topo);
			Assert.NotNull(plan.TopoStorage);
			Assert.Equal("red-sfm.xml", plan.TopoStorage!.Path);
			Assert.True(plan.Demand[0].IsResolved);
		}
	}
}
