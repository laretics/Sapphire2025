using Diamond.Topo;

namespace Diamond.Tests.Topo
{
	public class TopoXmlSerializerTests
	{
		[Fact]
		public void Load_toposfm227_HasExpectedPackageAndAxes()
		{
			TopoLayout layout = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);

			Assert.Equal("SFM", layout.Info.Name);
			Assert.Equal("4.1.0", layout.Info.Version);
			Assert.Equal(3, layout.Axes.Count);

			Axis? m1 = layout.FindAxisById("M1");
			Axis? t3 = layout.FindAxisById("T3");
			Axis? t2 = layout.FindAxisById("T2");

			Assert.NotNull(m1);
			Assert.NotNull(t3);
			Assert.NotNull(t2);

			Assert.Equal(166, m1!.Vertices.Count);
			Assert.Equal(1426, t3!.Vertices.Count);
			Assert.Equal(270, t2!.Vertices.Count);

			Assert.Equal(9, CountAnchors(m1));
			Assert.Equal(24, CountAnchors(t3));
			Assert.Equal(5, CountAnchors(t2));

			Assert.Equal(9, m1.Stations.Count);
			Assert.Equal(24, t3.Stations.Count);
			Assert.Equal(5, t2.Stations.Count);

			// En el sample Onice cada id de estación es único en la red (no hay enlaces por id).
			Assert.Equal(38, layout.Stations.Count);

			Assert.True(m1.IsBuilt);
			Assert.Equal(0L, m1.PK);
			Assert.Equal(8442L, m1.Length);
		}

		[Fact]
		public void Load_toposfm227_BuildsStationCatalog_PalmaOnM1()
		{
			TopoLayout layout = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);

			Station? palma = layout.FindStationById("40");
			Assert.NotNull(palma);
			Assert.Equal("Palma", palma!.Name);
			Assert.Equal("PMI", palma.Avr);

			Axis m1 = layout.FindAxisById("M1")!;
			StationOnAxis first = m1.Stations[0];
			Assert.Same(palma, first.Station);
			Assert.Equal(0L, first.PK);
		}

		[Fact]
		public void Load_toposfm227_M1_PalmaProjectsToPkZero()
		{
			TopoLayout layout = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			Axis m1 = layout.FindAxisById("M1")!;

			AxisProjection projection = m1.PKFromLocation(39.57611336140333, 2.65436975152172, 50.0);

			Assert.True(projection.Success);
			Assert.Equal(0L, projection.PK);
			Assert.True(projection.DistanceMeters < 0.5);
		}

		[Fact]
		public void SaveAndLoad_LegacyRoundTrip_PreservesAxesAndStations()
		{
			TopoLayout original = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			string tempPath = Path.Combine(Path.GetTempPath(), "diamond-topo-legacy-" + Guid.NewGuid().ToString("N") + ".xml");

			try
			{
				TopoXmlSerializer.Save(original, tempPath, TopoXmlFormat.Legacy);
				TopoLayout reloaded = TopoXmlSerializer.Load(tempPath);

				Assert.Equal(original.Info.Name, reloaded.Info.Name);
				Assert.Equal(original.Axes.Count, reloaded.Axes.Count);
				Assert.Equal(original.Stations.Count, reloaded.Stations.Count);

				int index = 0;
				while (index < original.Axes.Count)
				{
					Axis expected = original.Axes[index];
					Axis? actual = reloaded.FindAxisById(expected.Id);
					Assert.NotNull(actual);
					Assert.Equal(expected.Name, actual!.Name);
					Assert.Equal(expected.Vertices.Count, actual.Vertices.Count);
					Assert.Equal(CountAnchors(expected), CountAnchors(actual));
					Assert.Equal(expected.Stations.Count, actual.Stations.Count);
					index++;
				}
			}
			finally
			{
				if (File.Exists(tempPath))
				{
					File.Delete(tempPath);
				}
			}
		}

		[Fact]
		public void SaveAndLoad_CanonicalRoundTrip_PreservesCatalogAndReferences()
		{
			TopoLayout original = TopoXmlSerializer.Load(SamplePaths.TopoSfm227);
			string tempPath = Path.Combine(Path.GetTempPath(), "diamond-topo-canonical-" + Guid.NewGuid().ToString("N") + ".xml");

			try
			{
				TopoXmlSerializer.Save(original, tempPath, TopoXmlFormat.Canonical);

				string xmlText = File.ReadAllText(tempPath);
				Assert.Contains("<stations>", xmlText, StringComparison.Ordinal);
				Assert.Contains("station=\"40\"", xmlText, StringComparison.Ordinal);
				// En canónico la identidad va al catálogo; los point del poly no llevan name= embebido.
				bool pointHasEmbeddedName = false;
				string[] lines = xmlText.Split('\n');
				int lineIndex = 0;
				while (lineIndex < lines.Length)
				{
					string line = lines[lineIndex];
					if (line.Contains("<point ", StringComparison.Ordinal)
						&& line.Contains(" name=\"", StringComparison.Ordinal))
					{
						pointHasEmbeddedName = true;
						break;
					}

					lineIndex++;
				}

				Assert.False(pointHasEmbeddedName);

				TopoLayout reloaded = TopoXmlSerializer.Load(tempPath);

				Assert.Equal(original.Stations.Count, reloaded.Stations.Count);
				Station? palma = reloaded.FindStationById("40");
				Assert.NotNull(palma);
				Assert.Equal("Palma", palma!.Name);

				Axis m1 = reloaded.FindAxisById("M1")!;
				Assert.Same(palma, m1.Stations[0].Station);
				Assert.Equal(166, m1.Vertices.Count);
			}
			finally
			{
				if (File.Exists(tempPath))
				{
					File.Delete(tempPath);
				}
			}
		}

		[Fact]
		public void SharedStation_OnTwoAxes_IsSameInstance()
		{
			// El sample SFM no comparte ids entre ejes; validamos el modelo canónico en memoria + XML.
			TopoLayout layout = new TopoLayout();
			layout.Info.Name = "Test";

			Station hub = layout.GetOrAddStation("HUB");
			hub.Name = "Enlace";
			hub.Avr = "ENL";
			hub.Latitude = 39.6;
			hub.Longitude = 2.9;

			Axis a = new Axis();
			a.Id = "A";
			a.Name = "Eje A";
			AxisVertex a0 = new AxisVertex(39.6, 2.9, 0L);
			a0.Station = hub;
			a.AddVertex(a0);
			a.AddVertex(39.61, 2.91, 1000L);
			a.Rebuild();
			layout.AddAxis(a);

			Axis b = new Axis();
			b.Id = "B";
			b.Name = "Eje B";
			AxisVertex b0 = new AxisVertex(39.6, 2.9, 5000L);
			b0.Station = hub;
			b.AddVertex(b0);
			b.AddVertex(39.59, 2.92, 6000L);
			b.Rebuild();
			layout.AddAxis(b);

			Assert.Single(layout.Stations);
			Assert.Same(hub, a.Stations[0].Station);
			Assert.Same(hub, b.Stations[0].Station);
			Assert.Equal(0L, a.Stations[0].PK);
			Assert.Equal(5000L, b.Stations[0].PK);

			string tempPath = Path.Combine(Path.GetTempPath(), "diamond-topo-shared-" + Guid.NewGuid().ToString("N") + ".xml");
			try
			{
				TopoXmlSerializer.Save(layout, tempPath, TopoXmlFormat.Canonical);
				TopoLayout reloaded = TopoXmlSerializer.Load(tempPath);

				Assert.Single(reloaded.Stations);
				Station reloadedHub = reloaded.FindStationById("HUB")!;
				Assert.Same(reloadedHub, reloaded.FindAxisById("A")!.Stations[0].Station);
				Assert.Same(reloadedHub, reloaded.FindAxisById("B")!.Stations[0].Station);

				// Proyección legacy: se duplican atributos, pero al recargar se reúne la identidad.
				TopoXmlSerializer.Save(layout, tempPath, TopoXmlFormat.Legacy);
				TopoLayout fromLegacy = TopoXmlSerializer.Load(tempPath);
				Assert.Single(fromLegacy.Stations);
				Assert.Same(
					fromLegacy.FindAxisById("A")!.Stations[0].Station,
					fromLegacy.FindAxisById("B")!.Stations[0].Station);
			}
			finally
			{
				if (File.Exists(tempPath))
				{
					File.Delete(tempPath);
				}
			}
		}

		private static int CountAnchors(Axis axis)
		{
			int count = 0;
			int index = 0;
			while (index < axis.Vertices.Count)
			{
				if (axis.Vertices[index].IsAnchor)
				{
					count++;
				}

				index++;
			}

			return count;
		}
	}
}
