using Diamond.Controls.Rendering;
using Diamond.Topo;

namespace Diamond.Tests.Controls
{
	public class ConsignaSerieBTests
	{
		[Fact]
		public void Build_NumbersSequentially_PlusOnNew_SidesByTrack()
		{
			TopoLayout layout = BuildAxisWithStations();
			List<TemporarySpeedLimit> temps = new List<TemporarySpeedLimit>
			{
				TopoTemporaryLimits.FromSpan(
					"T3", 2000L, 3500L, 40, TemporaryLimitReason.Works, "Obra puente",
					TemporaryLimitTrack.Both, isNewCreation: true),
				TopoTemporaryLimits.FromSpan(
					"T3", 8000L, 9000L, 30, TemporaryLimitReason.Geometry, null,
					TemporaryLimitTrack.Track1, isNewCreation: false)
			};

			ConsignaSerieBDocument doc = ConsignaSerieBDocument.Build(layout, temps, "SFM");
			Assert.Single(doc.Axes);
			Assert.Equal(2, doc.EntryCount);
			Assert.Equal(1, doc.Axes[0].Entries[0].Number);
			Assert.True(doc.Axes[0].Entries[0].IsNew);
			Assert.True(doc.Axes[0].Entries[0].AppliesLeft);
			Assert.True(doc.Axes[0].Entries[0].AppliesRight);
			Assert.Equal(2, doc.Axes[0].Entries[1].Number);
			Assert.False(doc.Axes[0].Entries[1].IsNew);
			Assert.False(doc.Axes[0].Entries[1].AppliesLeft);
			Assert.True(doc.Axes[0].Entries[1].AppliesRight);
			Assert.Equal("A", doc.Axes[0].Entries[0].StationBefore);
			Assert.Equal("B", doc.Axes[0].Entries[0].StationAfter);
			Assert.True(doc.Axes[0].Rows.Count >= 3);
			Assert.True(doc.Axes[0].Rows[0].IsStation);
			Assert.Equal("A", doc.Axes[0].Rows[0].StationName);
			Assert.False(doc.Axes[0].Rows[1].IsStation);
			Assert.True(doc.Axes[0].Rows[2].IsStation);
			Assert.Equal("B", doc.Axes[0].Rows[2].StationName);
		}

		[Fact]
		public void Render_ContainsHeaderSidesAndPlus()
		{
			TopoLayout layout = BuildAxisWithStations();
			List<TemporarySpeedLimit> temps = new List<TemporarySpeedLimit>
			{
				TopoTemporaryLimits.FromSpan(
					"T3", 2000L, 3500L, 40, TemporaryLimitReason.Works, "Obra puente",
					TemporaryLimitTrack.Both, isNewCreation: true)
			};

			ConsignaSerieBDocument doc = ConsignaSerieBDocument.Build(
				layout, temps, "SFM demo", date: new DateTime(2026, 8, 17));
			IReadOnlyList<string> sheets = ConsignaSerieBSvgRenderer.RenderAllPages(doc);
			Assert.Single(sheets);
			Assert.Contains("CONSIGNA SERIE B", sheets[0], StringComparison.Ordinal);
			Assert.Contains("diamond-doc-logo", sheets[0], StringComparison.Ordinal);
			Assert.Contains("diamond-logo-gray", sheets[0], StringComparison.Ordinal);
			Assert.Contains("data:image/png;base64,", sheets[0], StringComparison.Ordinal);
			Assert.Contains("Consigna Serie B nº XX  (17/08/2026)", sheets[0], StringComparison.Ordinal);
			Assert.DoesNotContain("Deroga Consigna", sheets[0], StringComparison.Ordinal);
			Assert.Contains("↓ Vía II", sheets[0], StringComparison.Ordinal);
			Assert.Contains("Vía I ↑", sheets[0], StringComparison.Ordinal);
			Assert.Contains("Obras", sheets[0], StringComparison.Ordinal);
			Assert.Contains("Obra puente", sheets[0], StringComparison.Ordinal);
			Assert.Contains(">+</text>", sheets[0], StringComparison.Ordinal);
			Assert.Contains("diamond-circ-qr", sheets[0], StringComparison.Ordinal);
			Assert.Contains("Palma–Manacor", sheets[0], StringComparison.Ordinal);
			Assert.DoesNotContain("1 de 1", sheets[0], StringComparison.Ordinal);
			Assert.DoesNotContain("T.C.", sheets[0], StringComparison.Ordinal);
		}

		[Fact]
		public void Cover_ShowsNumberDateAndRepealOfPrevious()
		{
			TopoLayout layout = BuildAxisWithStations();
			List<TemporarySpeedLimit> temps = new List<TemporarySpeedLimit>
			{
				TopoTemporaryLimits.FromSpan(
					"T3", 2000L, 3500L, 40, TemporaryLimitReason.Works, null)
			};
			ConsignaSerieBDocument doc = ConsignaSerieBDocument.Build(
				layout, temps, "SFM",
				consignaNumber: "26/002",
				date: new DateTime(2026, 8, 17),
				previousNumber: "26/001");
			Assert.Equal("26/002", doc.ConsignaNumber);
			Assert.Equal("Consigna Serie B nº 26/002  (17/08/2026)", doc.CoverTitle);
			Assert.Equal("Deroga Consigna Serie B nº 26/001 y anteriores", doc.RepealLine);
			IReadOnlyList<string> sheets = ConsignaSerieBSvgRenderer.RenderAllPages(doc);
			Assert.Contains("Consigna Serie B nº 26/002  (17/08/2026)", sheets[0], StringComparison.Ordinal);
			Assert.Contains("Deroga Consigna Serie B nº 26/001 y anteriores", sheets[0], StringComparison.Ordinal);
			Assert.Contains("nº 26/002", sheets[0], StringComparison.Ordinal);
		}

		[Fact]
		public void EmptyAxis_IsOmitted_CoverOnly()
		{
			TopoLayout layout = BuildAxisWithStations();
			ConsignaSerieBDocument doc = ConsignaSerieBDocument.Build(
				layout, Array.Empty<TemporarySpeedLimit>(), "Vacío",
				date: new DateTime(2026, 8, 17));
			Assert.Empty(doc.Axes);
			Assert.Single(doc.Pages);
			Assert.Equal(ConsignaSerieBPageKind.Cover, doc.Pages[0].Kind);
			Assert.Empty(doc.Index);
			IReadOnlyList<string> sheets = ConsignaSerieBSvgRenderer.RenderAllPages(doc);
			Assert.Single(sheets);
			Assert.Contains("Consigna Serie B nº XX  (17/08/2026)", sheets[0], StringComparison.Ordinal);
			Assert.DoesNotContain("Deroga Consigna", sheets[0], StringComparison.Ordinal);
			Assert.DoesNotContain("Palma–Manacor", sheets[0], StringComparison.Ordinal);
			Assert.DoesNotContain("Vía II", sheets[0], StringComparison.Ordinal);
		}

		[Fact]
		public void TwoAxesWithLimits_CoverPlusTwoHalves()
		{
			TopoLayout layout = BuildTwoAxes();
			List<TemporarySpeedLimit> temps = new List<TemporarySpeedLimit>
			{
				TopoTemporaryLimits.FromSpan("T3", 2000L, 3000L, 40, TemporaryLimitReason.Works, null),
				TopoTemporaryLimits.FromSpan("M1", 500L, 800L, 30, TemporaryLimitReason.Weather, null)
			};
			ConsignaSerieBDocument doc = ConsignaSerieBDocument.Build(layout, temps, "SFM");
			Assert.Equal(2, doc.Axes.Count);
			Assert.Equal(3, doc.Pages.Count);
			Assert.Equal(ConsignaSerieBPageKind.Cover, doc.Pages[0].Kind);
			Assert.Equal(ConsignaSerieBPageKind.Axis, doc.Pages[1].Kind);
			Assert.Equal(ConsignaSerieBPageKind.Axis, doc.Pages[2].Kind);
			Assert.Equal(2, doc.Index.Count);
			Assert.Equal("Palma–Manacor", doc.Index[0].AxisName);
			Assert.Equal(2, doc.Index[0].PageNumber);
			Assert.Equal("Manacor", doc.Index[1].AxisName);
			Assert.Equal(3, doc.Index[1].PageNumber);
			IReadOnlyList<string> sheets = ConsignaSerieBSvgRenderer.RenderAllPages(doc);
			Assert.Equal(2, sheets.Count);
			Assert.Contains("Consigna Serie B nº XX", sheets[0], StringComparison.Ordinal);
			Assert.Contains("Palma–Manacor", sheets[0], StringComparison.Ordinal);
			Assert.Contains("Manacor", sheets[0], StringComparison.Ordinal);
		}

		[Fact]
		public void EmptyAxisAmongTwo_IsOmittedFromIndexAndPages()
		{
			TopoLayout layout = BuildTwoAxes();
			List<TemporarySpeedLimit> temps = new List<TemporarySpeedLimit>
			{
				TopoTemporaryLimits.FromSpan("T3", 2000L, 3000L, 40, TemporaryLimitReason.Works, null)
			};
			ConsignaSerieBDocument doc = ConsignaSerieBDocument.Build(layout, temps, "SFM");
			Assert.Single(doc.Axes);
			Assert.Equal("T3", doc.Axes[0].AxisId);
			Assert.Equal(2, doc.Pages.Count);
			Assert.Single(doc.Index);
			Assert.DoesNotContain(doc.Index, e => string.Equals(e.AxisName, "Manacor", StringComparison.Ordinal));
			IReadOnlyList<string> sheets = ConsignaSerieBSvgRenderer.RenderAllPages(doc);
			Assert.Single(sheets);
			Assert.DoesNotContain(">Manacor</text>", sheets[0], StringComparison.Ordinal);
		}

		[Fact]
		public void LongObservations_WrapFully_NoEllipsis()
		{
			TopoLayout layout = BuildAxisWithStations();
			string obs = "INICIOOBS "
				+ string.Join(" ", Enumerable.Range(1, 40).Select(i => "tramo" + i.ToString()))
				+ " FINOBS";
			List<TemporarySpeedLimit> temps = new List<TemporarySpeedLimit>
			{
				TopoTemporaryLimits.FromSpan(
					"T3", 2000L, 3500L, 40, TemporaryLimitReason.Works, obs,
					TemporaryLimitTrack.Both, isNewCreation: true)
			};

			ConsignaSerieBDocument doc = ConsignaSerieBDocument.Build(layout, temps, "SFM");
			IReadOnlyList<string> sheets = ConsignaSerieBSvgRenderer.RenderAllPages(doc);
			Assert.Single(sheets);
			Assert.Contains("INICIOOBS", sheets[0], StringComparison.Ordinal);
			Assert.Contains("FINOBS", sheets[0], StringComparison.Ordinal);
			Assert.DoesNotContain("INICIOOBS…", sheets[0], StringComparison.Ordinal);
			Assert.DoesNotContain("tramo1…", sheets[0], StringComparison.Ordinal);
			Assert.True(sheets[0].Split("tramo", StringSplitOptions.None).Length > 20);
		}

		[Fact]
		public void BlocksStayAtomicAcrossPages()
		{
			TopoLayout layout = BuildCorridor(18);
			List<TemporarySpeedLimit> temps = new List<TemporarySpeedLimit>();
			int i = 0;
			while (i < 17)
			{
				long lo = (i * 1000L) + 200L;
				temps.Add(TopoTemporaryLimits.FromSpan(
					"T3", lo, lo + 400L, 40, TemporaryLimitReason.Works, "Obs " + i.ToString()));
				i++;
			}

			ConsignaSerieBDocument doc = ConsignaSerieBDocument.Build(layout, temps, "SFM");
			int axisPages = 0;
			int pi = 0;
			while (pi < doc.Pages.Count)
			{
				if (doc.Pages[pi].Kind == ConsignaSerieBPageKind.Axis)
				{
					axisPages++;
					AssertPageBlocksAtomic(doc.Pages[pi]);
				}

				pi++;
			}

			Assert.True(axisPages >= 2);
			Assert.Contains("1 de ", doc.Pages[1].AxisHeaderText, StringComparison.Ordinal);
			Assert.DoesNotContain("1 de 1", ConcatAxisHeaders(doc), StringComparison.Ordinal);
			IReadOnlyList<string> sheets = ConsignaSerieBSvgRenderer.RenderAllPages(doc);
			Assert.Contains("1 de ", sheets[0], StringComparison.Ordinal);
			Assert.DoesNotContain("1 de 1", sheets[0], StringComparison.Ordinal);
		}

		[Fact]
		public void NumberAndPlusSitLeftOfTable()
		{
			TopoLayout layout = BuildAxisWithStations();
			List<TemporarySpeedLimit> temps = new List<TemporarySpeedLimit>
			{
				TopoTemporaryLimits.FromSpan(
					"T3", 2000L, 3500L, 40, TemporaryLimitReason.Works, "Obra puente",
					TemporaryLimitTrack.Both, isNewCreation: true)
			};

			ConsignaSerieBDocument doc = ConsignaSerieBDocument.Build(layout, temps, "SFM");
			IReadOnlyList<string> sheets = ConsignaSerieBSvgRenderer.RenderAllPages(doc);
			string svg = sheets[0];
			double plusX = FirstTextX(svg, ">+</text>");
			double tableX = FirstBodyRectX(svg);
			Assert.True(plusX > 0);
			Assert.True(tableX > plusX);
		}

		[Fact]
		public void StationNamesAreBlackOnWhite()
		{
			TopoLayout layout = BuildAxisWithStations();
			List<TemporarySpeedLimit> temps = new List<TemporarySpeedLimit>
			{
				TopoTemporaryLimits.FromSpan(
					"T3", 2000L, 3500L, 40, TemporaryLimitReason.Works, null)
			};
			ConsignaSerieBDocument doc = ConsignaSerieBDocument.Build(layout, temps, "SFM");
			string svg = ConsignaSerieBSvgRenderer.RenderAllPages(doc)[0];
			Assert.Contains(">A</text>", svg, StringComparison.Ordinal);
			Assert.Equal("#000", TextFill(svg, ">A</text>"));
			Assert.Equal("#000", TextFill(svg, ">B</text>"));
		}

		[Fact]
		public void VColumn_ShadesBySpeedThreshold()
		{
			TopoLayout layout = BuildAxisWithStations();
			List<TemporarySpeedLimit> temps = new List<TemporarySpeedLimit>
			{
				TopoTemporaryLimits.FromSpan(
					"T3", 2000L, 3000L, 50, TemporaryLimitReason.Works, null),
				TopoTemporaryLimits.FromSpan(
					"T3", 3100L, 4000L, 51, TemporaryLimitReason.Geometry, null)
			};
			ConsignaSerieBDocument doc = ConsignaSerieBDocument.Build(layout, temps, "SFM");
			string svg = ConsignaSerieBSvgRenderer.RenderAllPages(doc)[0];
			Assert.Contains("fill=\"#2b2b2b\"", svg, StringComparison.Ordinal);
			Assert.Contains("fill=\"#d9d9d9\"", svg, StringComparison.Ordinal);
			Assert.Equal("#ffffff", TextFill(svg, ">50</text>"));
			Assert.Equal("#000000", TextFill(svg, ">51</text>"));
		}

		[Fact]
		public void Limit_ShowsCreatedDateBelowComments()
		{
			TopoLayout layout = BuildAxisWithStations();
			TemporarySpeedLimit temp = TopoTemporaryLimits.FromSpan(
				"T3", 2000L, 3500L, 40, TemporaryLimitReason.Works, "Obra puente",
				TemporaryLimitTrack.Both, isNewCreation: true,
				createdAt: new DateTime(2026, 8, 17, 8, 30, 0, DateTimeKind.Unspecified));
			ConsignaSerieBDocument doc = ConsignaSerieBDocument.Build(
				layout, new[] { temp }, "SFM");
			string svg = ConsignaSerieBSvgRenderer.RenderAllPages(doc)[0];
			Assert.Contains("Obra puente", svg, StringComparison.Ordinal);
			Assert.Contains("(17-08-26)", svg, StringComparison.Ordinal);
		}

		[Fact]
		public void ConsecutiveLimits_KmSitOnRowEdges_WithJoinGap()
		{
			TopoLayout layout = BuildAxisWithStations();
			List<TemporarySpeedLimit> temps = new List<TemporarySpeedLimit>
			{
				TopoTemporaryLimits.FromSpan(
					"T3", 2000L, 2500L, 40, TemporaryLimitReason.Works, null),
				TopoTemporaryLimits.FromSpan(
					"T3", 2600L, 3000L, 30, TemporaryLimitReason.Geometry, null)
			};
			ConsignaSerieBDocument doc = ConsignaSerieBDocument.Build(layout, temps, "SFM");
			Assert.Equal(4, doc.Axes[0].Rows.Count);
			Assert.True(doc.Axes[0].Rows[1].Entry is not null);
			Assert.True(doc.Axes[0].Rows[2].Entry is not null);
			string svg = ConsignaSerieBSvgRenderer.RenderAllPages(doc)[0];
			Assert.Contains("font-size=\"9\"", svg, StringComparison.Ordinal);
			double yEndFirst = FirstTextY(svg, ">2.5</text>");
			double yStartSecond = FirstTextY(svg, ">2.6</text>");
			Assert.True(yEndFirst > 0);
			Assert.True(yStartSecond > yEndFirst);
			Assert.True(
				yStartSecond - yEndFirst >= 11,
				"El km final de una limitación y el inicial de la siguiente no deben pisarse.");
		}

		[Fact]
		public void LimitSpanningStations_ContainsInteriors_NoRepeatedNames()
		{
			TopoLayout layout = BuildThreeStations();
			List<TemporarySpeedLimit> temps = new List<TemporarySpeedLimit>
			{
				TopoTemporaryLimits.FromSpan(
					"T3", 2000L, 11000L, 20, TemporaryLimitReason.Works, null),
				TopoTemporaryLimits.FromSpan(
					"T3", 5000L, 11000L, 80, TemporaryLimitReason.Geometry, null)
			};

			ConsignaSerieBDocument doc = ConsignaSerieBDocument.Build(layout, temps, "SFM");
			Assert.Single(doc.Axes);
			IReadOnlyList<ConsignaSerieBRow> rows = doc.Axes[0].Rows;
			Assert.Equal(4, rows.Count);
			Assert.True(rows[0].IsStation);
			Assert.Equal("V.LLUC", rows[0].StationName);
			Assert.False(rows[1].IsStation);
			Assert.Equal(20, rows[1].Entry!.Limit.Speed);
			Assert.Single(rows[1].Entry.InteriorStations);
			Assert.Equal("PONT D'INCA", rows[1].Entry.InteriorStations[0]);
			Assert.False(rows[2].IsStation);
			Assert.Equal(80, rows[2].Entry!.Limit.Speed);
			Assert.Empty(rows[2].Entry.InteriorStations);
			Assert.True(rows[3].IsStation);
			Assert.Equal("PONT D'INCA NOU", rows[3].StationName);

			List<string> names = CollectStationNames(rows);
			Assert.Equal(3, names.Count);
			Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());

			string svg = ConsignaSerieBSvgRenderer.RenderAllPages(doc)[0];
			Assert.Contains("V.LLUC", svg, StringComparison.Ordinal);
			Assert.Contains("PONT D'INCA", svg, StringComparison.Ordinal);
			Assert.Contains("PONT D'INCA NOU", svg, StringComparison.Ordinal);
		}

		private static List<string> CollectStationNames(IReadOnlyList<ConsignaSerieBRow> rows)
		{
			List<string> names = new List<string>();
			int i = 0;
			while (i < rows.Count)
			{
				if (rows[i].IsStation)
				{
					names.Add(rows[i].StationName);
				}
				else if (rows[i].Entry is not null)
				{
					int k = 0;
					while (k < rows[i].Entry!.InteriorStations.Count)
					{
						names.Add(rows[i].Entry.InteriorStations[k]);
						k++;
					}
				}

				i++;
			}

			return names;
		}

		private static TopoLayout BuildThreeStations()
		{
			Station stA = new Station("VLL");
			stA.Name = "V.Lluc";
			stA.Avr = "VLL";
			Station stB = new Station("PIN");
			stB.Name = "Pont d'Inca";
			stB.Avr = "PIN";
			Station stC = new Station("PINN");
			stC.Name = "Pont d'Inca Nou";
			stC.Avr = "PINN";

			Axis axis = new Axis();
			axis.Id = "T3";
			axis.Name = "Palma–Inca";
			axis.Vmax = 100;
			axis.AddVertex(new AxisVertex(39.0, 2.0, 0L));
			AxisVertex vA = new AxisVertex(39.01, 2.01, 1000L);
			vA.Station = stA;
			AxisVertex vB = new AxisVertex(39.03, 2.03, 5000L);
			vB.Station = stB;
			AxisVertex vC = new AxisVertex(39.05, 2.05, 12000L);
			vC.Station = stC;
			axis.AddVertex(vA);
			axis.AddVertex(vB);
			axis.AddVertex(vC);
			axis.AddVertex(new AxisVertex(39.1, 2.1, 20000L));
			axis.Rebuild();

			TopoLayout layout = new TopoLayout();
			layout.AddStation(stA);
			layout.AddStation(stB);
			layout.AddStation(stC);
			layout.AddAxis(axis);
			return layout;
		}

		private static TopoLayout BuildAxisWithStations()
		{
			Station stA = new Station("A");
			stA.Name = "A";
			stA.Avr = "A";
			Station stB = new Station("B");
			stB.Name = "B";
			stB.Avr = "B";

			Axis axis = new Axis();
			axis.Id = "T3";
			axis.Name = "Palma–Manacor";
			axis.Vmax = 100;
			AxisVertex v0 = new AxisVertex(39.0, 2.0, 0L);
			AxisVertex vA = new AxisVertex(39.02, 2.02, 1000L);
			vA.Station = stA;
			AxisVertex vB = new AxisVertex(39.05, 2.05, 5000L);
			vB.Station = stB;
			AxisVertex v1 = new AxisVertex(39.1, 2.1, 20000L);
			axis.AddVertex(v0);
			axis.AddVertex(vA);
			axis.AddVertex(vB);
			axis.AddVertex(v1);
			axis.Rebuild();

			TopoLayout layout = new TopoLayout();
			layout.AddStation(stA);
			layout.AddStation(stB);
			layout.AddAxis(axis);
			return layout;
		}

		private static TopoLayout BuildTwoAxes()
		{
			TopoLayout layout = BuildAxisWithStations();
			Station stC = new Station("C");
			stC.Name = "C";
			Axis m1 = new Axis();
			m1.Id = "M1";
			m1.Name = "Manacor";
			m1.Vmax = 80;
			AxisVertex a0 = new AxisVertex(39.2, 3.0, 0L);
			AxisVertex aC = new AxisVertex(39.21, 3.01, 400L);
			aC.Station = stC;
			AxisVertex a1 = new AxisVertex(39.3, 3.1, 4000L);
			m1.AddVertex(a0);
			m1.AddVertex(aC);
			m1.AddVertex(a1);
			m1.Rebuild();
			layout.AddStation(stC);
			layout.AddAxis(m1);
			return layout;
		}

		private static TopoLayout BuildCorridor(int stationCount)
		{
			Axis axis = new Axis();
			axis.Id = "T3";
			axis.Name = "Corredor";
			axis.Vmax = 100;
			axis.AddVertex(new AxisVertex(39.0, 2.0, 0L));
			TopoLayout layout = new TopoLayout();
			int i = 0;
			while (i < stationCount)
			{
				string id = "S" + i.ToString("00");
				Station st = new Station(id);
				st.Name = id;
				AxisVertex v = new AxisVertex(39.0 + (i * 0.01), 2.0 + (i * 0.01), i * 1000L);
				v.Station = st;
				axis.AddVertex(v);
				layout.AddStation(st);
				i++;
			}

			axis.AddVertex(new AxisVertex(39.5, 2.5, stationCount * 1000L + 2000L));
			axis.Rebuild();
			layout.AddAxis(axis);
			return layout;
		}

		private static string ConcatAxisHeaders(ConsignaSerieBDocument doc)
		{
			System.Text.StringBuilder sb = new System.Text.StringBuilder();
			int i = 0;
			while (i < doc.Pages.Count)
			{
				if (doc.Pages[i].Kind == ConsignaSerieBPageKind.Axis)
				{
					sb.Append(doc.Pages[i].AxisHeaderText);
					sb.Append('|');
				}

				i++;
			}

			return sb.ToString();
		}

		private static void AssertPageBlocksAtomic(ConsignaSerieBPage page)
		{
			if (page.Kind != ConsignaSerieBPageKind.Axis)
			{
				return;
			}

			int i = 0;
			while (i < page.Rows.Count)
			{
				ConsignaSerieBRow row = page.Rows[i];
				if (!row.IsStation && row.Entry is not null)
				{
					if (!string.IsNullOrEmpty(row.Entry.StationBefore))
					{
						Assert.True(
							HasStationBefore(page.Rows, i, row.Entry.StationBefore),
							"La estación anterior del bloque debe ir en la misma hoja.");
					}

					if (!string.IsNullOrEmpty(row.Entry.StationAfter))
					{
						Assert.True(
							HasStationAfter(page.Rows, i, row.Entry.StationAfter),
							"La estación siguiente del bloque debe ir en la misma hoja.");
					}
				}

				i++;
			}
		}

		private static bool HasStationBefore(
			IReadOnlyList<ConsignaSerieBRow> rows,
			int index,
			string name)
		{
			int i = index - 1;
			while (i >= 0)
			{
				if (RowHasStation(rows[i], name))
				{
					return true;
				}

				i--;
			}

			return false;
		}

		private static bool HasStationAfter(
			IReadOnlyList<ConsignaSerieBRow> rows,
			int index,
			string name)
		{
			int i = index + 1;
			while (i < rows.Count)
			{
				if (RowHasStation(rows[i], name))
				{
					return true;
				}

				i++;
			}

			return false;
		}

		private static bool RowHasStation(ConsignaSerieBRow row, string name)
		{
			if (row.IsStation && string.Equals(row.StationName, name, StringComparison.Ordinal))
			{
				return true;
			}

			if (row.Entry is null)
			{
				return false;
			}

			int i = 0;
			while (i < row.Entry.InteriorStations.Count)
			{
				if (string.Equals(row.Entry.InteriorStations[i], name, StringComparison.Ordinal))
				{
					return true;
				}

				i++;
			}

			return false;
		}

		private static double FirstTextX(string svg, string marker)
		{
			return TextAttribute(svg, marker, "x=\"");
		}

		private static double FirstTextY(string svg, string marker)
		{
			return TextAttribute(svg, marker, "y=\"");
		}

		private static string TextFill(string svg, string marker)
		{
			int at = svg.IndexOf(marker, StringComparison.Ordinal);
			if (at < 0)
			{
				return string.Empty;
			}

			int open = svg.LastIndexOf("<text", at, StringComparison.Ordinal);
			if (open < 0)
			{
				return string.Empty;
			}

			int fillAt = svg.IndexOf("fill=\"", open, StringComparison.Ordinal);
			if (fillAt < 0 || fillAt > at)
			{
				return string.Empty;
			}

			int start = fillAt + 6;
			int end = svg.IndexOf('"', start);
			if (end < 0)
			{
				return string.Empty;
			}

			return svg.Substring(start, end - start);
		}

		private static double TextAttribute(string svg, string marker, string attr)
		{
			int at = svg.IndexOf(marker, StringComparison.Ordinal);
			if (at < 0)
			{
				return -1;
			}

			int open = svg.LastIndexOf("<text", at, StringComparison.Ordinal);
			if (open < 0)
			{
				return -1;
			}

			int attrAt = svg.IndexOf(attr, open, StringComparison.Ordinal);
			if (attrAt < 0 || attrAt > at)
			{
				return -1;
			}

			int start = attrAt + attr.Length;
			int end = svg.IndexOf('"', start);
			if (end < 0)
			{
				return -1;
			}

			return double.Parse(svg.Substring(start, end - start), System.Globalization.CultureInfo.InvariantCulture);
		}

		private static double FirstBodyRectX(string svg)
		{
			const string fillNone = "fill=\"none\"";
			int at = svg.IndexOf(fillNone, StringComparison.Ordinal);
			if (at < 0)
			{
				return -1;
			}

			int open = svg.LastIndexOf("<rect", at, StringComparison.Ordinal);
			if (open < 0)
			{
				return -1;
			}

			int xAttr = svg.IndexOf("x=\"", open, StringComparison.Ordinal);
			if (xAttr < 0 || xAttr > at)
			{
				return -1;
			}

			int start = xAttr + 3;
			int end = svg.IndexOf('"', start);
			if (end < 0)
			{
				return -1;
			}

			return double.Parse(svg.Substring(start, end - start), System.Globalization.CultureInfo.InvariantCulture);
		}
	}
}
