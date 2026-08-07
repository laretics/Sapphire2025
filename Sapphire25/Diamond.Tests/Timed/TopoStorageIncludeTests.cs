using Diamond.Timed;
using Diamond.Topo;

namespace Diamond.Tests.Timed
{
	public class TopoStorageIncludeTests
	{
		public TopoStorageIncludeTests()
		{
			TopoStorage.ClearMemoryCatalog();
			TopoStorage.SetDefaultIncludeResolver(null);
		}

		[Fact]
		public void Parse_Include_CapturesTopoPath()
		{
			DemandCompileResult result = DemandScriptParser.Parse("""
				include toposfm227
				plan "demo"
				req PMI -> MAN 06:00-07:00 as R1
				""");

			Assert.True(result.Success, string.Join("; ", result.Errors));
			Assert.Equal("toposfm227.xml", result.IncludedTopoPath);
			Assert.Equal("demo", result.PlanName);
			Assert.Single(result.Requirements);
		}

		[Fact]
		public void Parse_Include_NameWithoutExtension_AssumesXml()
		{
			DemandCompileResult bare = DemandScriptParser.Parse("include toposfm227");
			Assert.True(bare.Success, string.Join("; ", bare.Errors));
			Assert.Equal("toposfm227.xml", bare.IncludedTopoPath);

			DemandCompileResult quoted = DemandScriptParser.Parse("include \"toposfm227\"");
			Assert.True(quoted.Success, string.Join("; ", quoted.Errors));
			Assert.Equal("toposfm227.xml", quoted.IncludedTopoPath);

			DemandCompileResult already = DemandScriptParser.Parse("include \"toposfm227.XML\"");
			Assert.True(already.Success, string.Join("; ", already.Errors));
			Assert.Equal("toposfm227.XML", already.IncludedTopoPath);
		}

		[Fact]
		public void Parse_IncludeTopoKeyword_AndIncluirAlias()
		{
			DemandCompileResult a = DemandScriptParser.Parse(
				"include topo \"Samples/Onice/toposfm227\"");
			Assert.True(a.Success, string.Join("; ", a.Errors));
			Assert.Equal("Samples/Onice/toposfm227.xml", a.IncludedTopoPath);

			DemandCompileResult b = DemandScriptParser.Parse(
				"incluir \"otro.xml\"");
			Assert.True(b.Success, string.Join("; ", b.Errors));
			Assert.Equal("otro.xml", b.IncludedTopoPath);
		}

		[Fact]
		public void Parse_DuplicateInclude_DifferentPath_Errors()
		{
			DemandCompileResult result = DemandScriptParser.Parse("""
				include "a.xml"
				include "b.xml"
				""");

			Assert.False(result.Success);
			Assert.Contains("solo se admite un include", string.Join(" ", result.Errors), StringComparison.OrdinalIgnoreCase);
		}

		[Fact]
		public void Parse_IncludeIndented_Errors()
		{
			DemandCompileResult result = DemandScriptParser.Parse("  include \"a.xml\"");
			Assert.False(result.Success);
			Assert.Contains("nivel raíz", string.Join(" ", result.Errors), StringComparison.OrdinalIgnoreCase);
		}

		[Fact]
		public void TopoStorage_LoadFromXml_SampleSfm()
		{
			string dir = Path.GetDirectoryName(SamplePaths.TopoSfm227)!;
			TopoStorage storage = TopoStorage.LoadFromXml("toposfm227.xml", dir);

			Assert.Equal("toposfm227.xml", storage.Path);
			Assert.True(File.Exists(storage.ResolvedPath));
			Assert.NotNull(storage.Layout.FindAxisById("T3"));
		}

		[Fact]
		public void Plan_CompileDemand_Include_LoadsTopoAndResolvesStations()
		{
			Plan plan = new Plan();
			plan.ScriptBaseDirectory = Path.GetDirectoryName(SamplePaths.TopoSfm227) ?? string.Empty;
			plan.EnsureDefaultTrainSpecs();

			string script = """
				include toposfm227
				plan "include demo"
				require 2/h PMI -> MAN 06:00-22:00 as R1
				""";

			DemandCompileResult result = plan.CompileDemand(script);
			Assert.True(result.Success, string.Join("; ", result.Errors));
			Assert.NotNull(plan.Topo);
			Assert.NotNull(plan.TopoStorage);
			Assert.Equal("toposfm227.xml", plan.TopoStorage!.Path);
			Assert.True(plan.Demand[0].IsResolved);
			Assert.Equal("Palma", plan.Demand[0].FromStation!.Name);
			Assert.Equal("Manacor", plan.Demand[0].ToStation!.Name);
		}

		[Fact]
		public void EnsureXmlExtension_AppendsOnlyWhenMissing()
		{
			Assert.Equal("toposfm227.xml", TopoStorage.EnsureXmlExtension("toposfm227"));
			Assert.Equal("toposfm227.xml", TopoStorage.EnsureXmlExtension("toposfm227.xml"));
			Assert.Equal("toposfm227.XML", TopoStorage.EnsureXmlExtension("toposfm227.XML"));
			Assert.Equal(@"C:\data\red.xml", TopoStorage.EnsureXmlExtension(@"C:\data\red"));
		}

		[Fact]
		public void Plan_CompileDemand_Include_MissingFile_Fails()
		{
			Plan plan = new Plan();
			plan.ScriptBaseDirectory = Path.GetDirectoryName(SamplePaths.TopoSfm227) ?? string.Empty;

			DemandCompileResult result = plan.CompileDemand(
				"include \"no-existe-xyz.xml\"\nrequire 1/h PMI -> MAN");

			Assert.False(result.Success);
			Assert.Contains("no se encontró", string.Join(" ", result.Errors), StringComparison.OrdinalIgnoreCase);
			Assert.Null(plan.TopoStorage);
		}

		[Fact]
		public void Plan_CompileDemand_AbsoluteInclude_WorksWithoutBaseDirectory()
		{
			Plan plan = new Plan();
			plan.EnsureDefaultTrainSpecs();

			string abs = SamplePaths.TopoSfm227.Replace('\\', '/');
			string script =
				"include \"" + abs + "\"\n"
				+ "plan \"abs\"\n"
				+ "require 1/h PMI -> MAN 06:00-07:00 as R1\n";

			DemandCompileResult result = plan.CompileDemand(script);
			Assert.True(result.Success, string.Join("; ", result.Errors));
			Assert.NotNull(plan.Topo);
			Assert.True(plan.Demand[0].IsResolved);
		}
	}
}
