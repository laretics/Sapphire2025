using Diamond.Project;
using Diamond.Timed;
using Diamond.Topo;

namespace Diamond.Tests.Project
{
	public class ProjectCompilerTests
	{
		[Fact]
		public void Compile_FromMesh_FactorizesAsimilationsAndMaterializesCalls()
		{
			string script = """
				days lab
				  req both ways every 60 min PMI -> MAN 06:00-10:00 as R-T3
				    stops 30s
				""";

			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			SfmDemoInfrastructure.Apply(topo);
			Plan plan = new Plan(topo);
			plan.Name = "Test compile";
			plan.Id = "test-plan";
			plan.EnsureDefaultTrainSpecs();
			Assert.True(plan.CompileDemand(script).Success, "compile demand");

			Mesh mesh = new MeshPlanner(plan).Solve(DayOfWeek.Monday);
			Assert.NotEmpty(mesh.Circulations);

			Diamond.Project.Project project = ProjectCompiler.Compile(plan, mesh);

			Assert.Equal("Test compile", project.Name);
			Assert.Equal("test-plan", project.Id);
			Assert.Equal(DayOfWeek.Monday, project.PlanningDay);
			Assert.Equal(mesh.Circulations.Count, project.Circulations.Count);
			Assert.True(project.Asimilations.Count > 0);
			Assert.True(project.Asimilations.Count <= mesh.Circulations.Count);

			// Cada asimilación del proyecto tiene al menos origen y destino.
			Assert.All(project.Asimilations, a =>
			{
				Assert.True(a.Calls.Count >= 2);
				Assert.True(a.Calls[0].IsOrigin);
				Assert.True(a.Calls[a.Calls.Count - 1].IsDestination);
				Assert.Equal(a.TotalTime, a.Calls[a.Calls.Count - 1].ArrivalOffset);
				Assert.NotEmpty(a.Circulations);
			});

			// Horarios absolutos = salida + offset
			Diamond.Project.Circulation first = project.Circulations[0];
			Assert.Equal(first.Departure, first.Calls[0].Departure);
			Assert.Equal(first.Arrival, first.Calls[first.Calls.Count - 1].Arrival);
			Assert.Equal(first.Asimilation.Origin.DisplayCode, first.Origin.DisplayCode);

			// Numeración: si la malla tiene service numbers, el proyecto los conserva
			if (mesh.Circulations.Any(c => c.ServiceNumber > 0))
			{
				Assert.Contains(project.Circulations, c => c.ServiceNumber > 0);
			}
		}

		[Fact]
		public void Compile_AfterDelete_OnlyKeepsRemainingTrains()
		{
			string script = """
				days lab
				  req 1/h PMI -> MAN 06:00-10:00 as R-base
				  delete 07:00-09:00
				""";

			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			SfmDemoInfrastructure.Apply(topo);
			Plan plan = new Plan(topo);
			plan.EnsureDefaultTrainSpecs();
			Assert.True(plan.CompileDemand(script).Success);

			Mesh mesh = new MeshPlanner(plan).Solve(DayOfWeek.Monday);
			Diamond.Project.Project project = ProjectCompiler.Compile(mesh);

			Assert.Equal(mesh.Circulations.Count, project.Circulations.Count);
			Assert.DoesNotContain(
				project.Circulations,
				c => c.Departure >= new TimeSpan(7, 0, 0) && c.Departure < new TimeSpan(9, 0, 0));
		}
	}
}
