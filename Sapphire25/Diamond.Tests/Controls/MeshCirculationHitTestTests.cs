using Diamond.Controls.Rendering;
using Diamond.Motion;
using Diamond.Timed;
using Diamond.Topo;

namespace Diamond.Tests.Controls
{
	public class MeshCirculationHitTestTests
	{
		[Fact]
		public void TryPickNearest_ReturnsNullOutsideInfluence()
		{
			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			SfmDemoInfrastructure.Apply(topo);
			Axis t3 = topo.FindAxisById("T3")!;
			RouteView view = RouteView.FromAxis(t3);

			Mesh empty = new Mesh();
			MeshSvgDrawOptions options = MeshSvgDrawOptions.Create(externalStationColumn: true);

			Circulation? hit = MeshCirculationHitTest.TryPickNearest(
				empty, view,
				TimeSpan.FromHours(6), TimeSpan.FromHours(14),
				view.PK, view.PKEnd,
				1160, 900, options,
				500, 400, 14);

			Assert.Null(hit);
		}
	}
}
