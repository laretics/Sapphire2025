using Diamond.Controls.Rendering;
using Diamond.Timed;
using Diamond.Topo;

namespace Diamond.Tests.Controls
{
	public class MeshYScaleTests
	{
		[Fact]
		public void Linear_PkToY_IsProportionalToPk()
		{
			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			SfmDemoInfrastructure.Apply(topo);
			RouteView view = RouteView.FromAxis(topo.FindAxisById("T3")!);

			long pkMin = view.PK;
			long pkMax = view.PKEnd;
			MeshYScale scale = MeshYScale.Create(MeshYScaleMode.LinearPk, view, pkMin, pkMax);

			const double plotTop = 36;
			const double plotH = 800;
			double yMin = scale.PkToY(pkMin, plotTop, plotH);
			double yMax = scale.PkToY(pkMax, plotTop, plotH);
			double yMid = scale.PkToY((pkMin + pkMax) / 2, plotTop, plotH);

			// PK alto arriba (y menor), PK bajo abajo (y mayor).
			Assert.True(yMax < yMin);
			Assert.InRange(yMid, Math.Min(yMin, yMax) - 1, Math.Max(yMin, yMax) + 1);
			Assert.Equal(plotTop + plotH, yMin, 3);
			Assert.Equal(plotTop, yMax, 3);
		}

		[Fact]
		public void Stepped_StationsAreEquidistantInScreenSpace()
		{
			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			SfmDemoInfrastructure.Apply(topo);
			RouteView view = RouteView.FromAxis(topo.FindAxisById("T3")!);

			long pkMin = view.PK;
			long pkMax = view.PKEnd;
			MeshYScale scale = MeshYScale.Create(MeshYScaleMode.SteppedSingular, view, pkMin, pkMax);

			Assert.Equal(MeshYScaleMode.SteppedSingular, scale.Mode);
			Assert.True(scale.Breaks.Count >= 3, "Se esperan singulares intermedios (estaciones y/o límites)");

			const double plotTop = 0;
			const double plotH = 1000;
			// Los breaks consecutivos deben mapear a pasos iguales de Y.
			double expectedStep = plotH / (scale.Breaks.Count - 1);
			int i = 0;
			while (i < scale.Breaks.Count - 1)
			{
				double y0 = scale.PkToY(scale.Breaks[i], plotTop, plotH);
				double y1 = scale.PkToY(scale.Breaks[i + 1], plotTop, plotH);
				// PK creciente → Y decreciente; distancia de pantalla = expectedStep.
				Assert.Equal(expectedStep, y0 - y1, 3);
				i++;
			}
		}

		[Fact]
		public void Stepped_IncludesStationsAndRoundTripsY()
		{
			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			SfmDemoInfrastructure.Apply(topo);
			RouteView view = RouteView.FromAxis(topo.FindAxisById("T3")!);

			long pkMin = view.PK;
			long pkMax = view.PKEnd;
			MeshYScale scale = MeshYScale.Create(MeshYScaleMode.SteppedSingular, view, pkMin, pkMax);

			// Cada estación de la vista dentro del rango es un break.
			int stationsInRange = 0;
			int si = 0;
			while (si < view.Stations.Count)
			{
				long pk = view.Stations[si].PK;
				if (pk >= pkMin && pk <= pkMax)
				{
					stationsInRange++;
					Assert.Contains(pk, scale.Breaks);
				}

				si++;
			}

			Assert.True(stationsInRange >= 2);

			const double plotTop = 10;
			const double plotH = 500;
			// Round-trip en extremos y en un break intermedio.
			Assert.Equal(pkMin, scale.YToPk(scale.PkToY(pkMin, plotTop, plotH), plotTop, plotH));
			Assert.Equal(pkMax, scale.YToPk(scale.PkToY(pkMax, plotTop, plotH), plotTop, plotH));
			if (scale.Breaks.Count >= 3)
			{
				long midBreak = scale.Breaks[scale.Breaks.Count / 2];
				Assert.Equal(midBreak, scale.YToPk(scale.PkToY(midBreak, plotTop, plotH), plotTop, plotH));
			}
		}

		[Fact]
		public void Stepped_CollectsSpeedFrontiersWhenPresent()
		{
			// Eje sintético con dos límites de velocidad y dos estaciones.
			Axis axis = new Axis();
			axis.Id = "X";
			axis.Name = "Test";
			// Lineal base: PK 0–10000 (LongAxis usa metros).
			// Axis se construye típicamente vía XML; aquí usamos FromAxis solo si hay datos.
			// En su lugar verificamos BuildSingularBreaks con un RouteView real y FixedLimits.
			TopoLayout topo = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			SfmDemoInfrastructure.Apply(topo);
			Axis t3 = topo.FindAxisById("T3")!;
			// Añadir un límite de velocidad artificial con frontiers conocidas.
			t3.FixedLimits.Add(80, 1000L, 5000L);

			RouteView view = RouteView.FromAxis(t3);
			long[] breaks = MeshYScale.BuildSingularBreaks(view, view.PK, view.PKEnd);

			Assert.Contains(1000L, breaks);
			Assert.Contains(5000L, breaks);
		}
	}
}
