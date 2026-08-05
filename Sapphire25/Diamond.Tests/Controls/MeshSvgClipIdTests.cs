using System.Text.RegularExpressions;
using Diamond.Controls.Rendering;
using Diamond.Motion;
using Diamond.Timed;
using Diamond.Topo;

namespace Diamond.Tests.Controls
{
	public class MeshSvgClipIdTests
	{
		[Fact]
		public void RenderContent_TwoSvgs_UseDistinctClipPathIds()
		{
			// Regresión: id fijo "plotClip" en pantalla + impresión hacía que url(#plotClip)
			// resolviera al clip de la malla en pantalla (más pequeña) y recortara las trazas.
			(Mesh mesh, RouteView view) = BuildTinyMesh();

			string a = MeshSvgRenderer.RenderContent(
				mesh, view,
				TimeSpan.FromHours(6), TimeSpan.FromHours(10),
				0, 20000,
				width: 800, height: 500,
				MeshSvgDrawOptions.Full);

			string b = MeshSvgRenderer.RenderContent(
				mesh, view,
				TimeSpan.FromHours(6), TimeSpan.FromHours(10),
				0, 20000,
				width: 2100, height: 1485,
				MeshSvgDrawOptions.Create(paperTheme: true));

			Match ma = Regex.Match(a, "clipPath id=\"(plotClip_[a-f0-9]+)\"");
			Match mb = Regex.Match(b, "clipPath id=\"(plotClip_[a-f0-9]+)\"");
			Assert.True(ma.Success, "SVG A debe tener clipPath con id único");
			Assert.True(mb.Success, "SVG B debe tener clipPath con id único");
			Assert.NotEqual(ma.Groups[1].Value, mb.Groups[1].Value);

			Assert.Contains("url(#" + ma.Groups[1].Value + ")", a, StringComparison.Ordinal);
			Assert.Contains("url(#" + mb.Groups[1].Value + ")", b, StringComparison.Ordinal);
			Assert.DoesNotContain("id=\"plotClip\"", a, StringComparison.Ordinal);
			Assert.DoesNotContain("id=\"plotClip\"", b, StringComparison.Ordinal);
		}

		private static (Mesh Mesh, RouteView View) BuildTinyMesh()
		{
			Station stA = new Station("A");
			stA.Name = "A";
			stA.Avr = "A";
			Station stB = new Station("B");
			stB.Name = "B";
			stB.Avr = "B";

			Axis axis = new Axis();
			axis.Id = "X1";
			axis.Vmax = 100;
			AxisVertex v0 = new AxisVertex(39.0, 2.0, 0L);
			v0.Station = stA;
			AxisVertex v1 = new AxisVertex(39.1, 2.1, 20000L);
			v1.Station = stB;
			axis.AddVertex(v0);
			axis.AddVertex(v1);
			axis.Rebuild();
			axis.SetCantonFrontiers(new long[] { 0L, 20000L });
			axis.DefaultTrackCount = 2;

			TopoLayout topo = new TopoLayout();
			topo.AddStation(stA);
			topo.AddStation(stB);
			topo.AddAxis(axis);

			Plan plan = new Plan(topo);
			plan.EnsureDefaultTrainSpecs();
			plan.DemandScript = "req A -> B 06:00-08:00 as R1\n  days lab\n";
			Assert.True(plan.CompileDemand().Success);
			Mesh mesh = new MeshPlanner(plan).Solve(DayOfWeek.Monday);
			Assert.NotEmpty(mesh.Circulations);
			return (mesh, RouteView.FromAxis(axis));
		}
	}
}
