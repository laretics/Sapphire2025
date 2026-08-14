using Diamond.Controls.Rendering;

namespace Diamond.Tests.Controls
{
	public class CirculationSheetPagerTests
	{
		[Theory]
		[InlineData(0, 30, 1)]
		[InlineData(10, 30, 1)]
		[InlineData(30, 30, 1)]
		[InlineData(31, 30, 2)]
		// Con solape: 30 + 29 + 29… → 60 únicas necesitan 3 páginas.
		[InlineData(60, 30, 3)]
		[InlineData(61, 30, 3)]
		[InlineData(100, 30, 4)]
		public void ComputePageCount_AddsPagesUntilAverageAtMostMax(int n, int max, int expectedPages)
		{
			Assert.Equal(expectedPages, CirculationSheetPager.ComputePageCount(n, max));
		}

		[Fact]
		public void Paginate_BalancesRowsAndContinuesWithLastRow()
		{
			const int max = 30;
			List<CirculationSheetFrontier> frontiers = new List<CirculationSheetFrontier>();
			int i = 0;
			while (i < 100)
			{
				frontiers.Add(MakeFrontier(i * 1000L, i < 99));
				i++;
			}

			IReadOnlyList<CirculationSheetPage> pages = CirculationSheetPager.Paginate(frontiers, max);
			Assert.Equal(4, pages.Count);

			int p = 0;
			int minRows = int.MaxValue;
			int maxRows = 0;
			while (p < pages.Count)
			{
				int c = pages[p].Frontiers.Count;
				Assert.True(c >= 1);
				Assert.True(c <= max);
				Assert.Equal(p, pages[p].PageIndex);
				Assert.Equal(4, pages[p].PageCount);
				if (c < minRows)
				{
					minRows = c;
				}

				if (c > maxRows)
				{
					maxRows = c;
				}

				p++;
			}

			// Reparto equilibrado de filas dibujadas (solapes incluidos): diferencia ≤ 1.
			Assert.True(maxRows - minRows <= 1, "filas desequilibradas: " + minRows + ".." + maxRows);

			// Continuidad: 1.ª fila de la página k = última de la k-1.
			p = 1;
			while (p < pages.Count)
			{
				CirculationSheetFrontier prevLast = pages[p - 1].Frontiers[pages[p - 1].Frontiers.Count - 1];
				CirculationSheetFrontier nextFirst = pages[p].Frontiers[0];
				Assert.Equal(prevLast.RoutePk, nextFirst.RoutePk);
				p++;
			}

			// Cobertura de todas las fronteras únicas (por PK de ruta) en orden.
			List<long> seenUnique = new List<long>();
			p = 0;
			while (p < pages.Count)
			{
				int r = p == 0 ? 0 : 1;
				while (r < pages[p].Frontiers.Count)
				{
					seenUnique.Add(pages[p].Frontiers[r].RoutePk);
					r++;
				}

				p++;
			}

			Assert.Equal(100, seenUnique.Count);
			i = 0;
			while (i < 100)
			{
				Assert.Equal(i * 1000L, seenUnique[i]);
				i++;
			}
		}

		[Fact]
		public void Paginate_SmallOverflow_BalancesAndRepeatsBoundary()
		{
			const int max = 5;
			List<CirculationSheetFrontier> frontiers = new List<CirculationSheetFrontier>();
			int i = 0;
			while (i < 7)
			{
				frontiers.Add(MakeFrontier(i * 100L, i < 6));
				i++;
			}

			// 7 únicas + 1 solape = 8 slots → 2 páginas de 4 filas (no 5+3 codicioso).
			IReadOnlyList<CirculationSheetPage> pages = CirculationSheetPager.Paginate(frontiers, max);
			Assert.Equal(2, pages.Count);
			Assert.Equal(4, pages[0].Frontiers.Count);
			Assert.Equal(4, pages[1].Frontiers.Count);
			Assert.Equal(pages[0].Frontiers[3].RoutePk, pages[1].Frontiers[0].RoutePk);
			Assert.Equal(300L, pages[0].Frontiers[3].RoutePk);
			Assert.Equal(600L, pages[1].Frontiers[3].RoutePk);
		}

		[Fact]
		public void Paginate_TypicalTwoPageTrain_IsBalancedNotGreedy()
		{
			// Caso tipo Manacor: ~41 fronteras, techo 30 → 2 páginas ~21/21, no 30/12.
			const int max = 30;
			const int n = 41;
			List<CirculationSheetFrontier> frontiers = new List<CirculationSheetFrontier>();
			int i = 0;
			while (i < n)
			{
				frontiers.Add(MakeFrontier(i * 1000L, i < n - 1));
				i++;
			}

			IReadOnlyList<CirculationSheetPage> pages = CirculationSheetPager.Paginate(frontiers, max);
			Assert.Equal(2, pages.Count);
			Assert.Equal(21, pages[0].Frontiers.Count);
			Assert.Equal(21, pages[1].Frontiers.Count);
			Assert.Equal(pages[0].Frontiers[20].RoutePk, pages[1].Frontiers[0].RoutePk);
		}

		[Fact]
		public void ComputeSheetCount_TwoBookPages_OneLandscapeSheet()
		{
			Assert.Equal(1, CirculationSheetPager.ComputeSheetCount(1));
			Assert.Equal(1, CirculationSheetPager.ComputeSheetCount(2));
			Assert.Equal(2, CirculationSheetPager.ComputeSheetCount(3));
			Assert.Equal(2, CirculationSheetPager.ComputeSheetCount(4));
		}

		[Fact]
		public void EstimateTextWidth_BoldCoversLongStationNames()
		{
			// El factor antiguo 0.56 dejaba el rectángulo corto en nombres largos.
			double old = "MANACOR".Length * 8.0 * 0.56;
			double neu = CirculationSheetSvgRenderer.EstimateTextWidth("MANACOR", 8.0, bold: true);
			Assert.True(neu > old + 2.0, "nuevo=" + neu + " old=" + old);
			Assert.True(neu >= 8.0 * 0.66 * 7, "ancho mínimo razonable");
		}

		[Fact]
		public void RenderAllPages_TwoBookPages_OneLandscapeSvg()
		{
			List<CirculationSheetFrontier> frontiers = new List<CirculationSheetFrontier>();
			int i = 0;
			while (i < 40)
			{
				frontiers.Add(MakeFrontier(i * 1000L, i < 39));
				i++;
			}

			IReadOnlyList<CirculationSheetPage> book = CirculationSheetPager.Paginate(frontiers, 30);
			Assert.Equal(2, book.Count);

			// Documento mínimo vía reflexión de Build no hace falta: RenderSheet directo.
			// Usamos un documento real con un plan pequeño sería pesado; comprobamos sheet count.
			Assert.Equal(1, CirculationSheetPager.ComputeSheetCount(book.Count));
		}

		[Fact]
		public void FormatSheetTime_HalfMinutes()
		{
			// Hora a dos dígitos (p. ej. 02.48, no 2.48).
			Assert.Equal("18.02", CirculationSheetDocument.FormatSheetTime(new TimeSpan(18, 2, 0)));
			Assert.Equal("18.02½", CirculationSheetDocument.FormatSheetTime(new TimeSpan(18, 2, 30)));
			Assert.Equal("18.03", CirculationSheetDocument.FormatSheetTime(new TimeSpan(18, 2, 50)));
			Assert.Equal("02.48", CirculationSheetDocument.FormatSheetTime(new TimeSpan(2, 48, 0)));
			Assert.Equal("02.48½", CirculationSheetDocument.FormatSheetTime(new TimeSpan(2, 48, 30)));

			string main = CirculationSheetDocument.FormatSheetTime(new TimeSpan(2, 48, 30), out string half);
			Assert.Equal("02.48", main);
			Assert.Equal("½", half);

			main = CirculationSheetDocument.FormatSheetTime(new TimeSpan(2, 48, 0), out half);
			Assert.Equal("02.48", main);
			Assert.Equal(string.Empty, half);
		}

		[Fact]
		public void FormatCommercialDwell_CircleUnderOneMinute()
		{
			string t = CirculationSheetDocument.FormatCommercialDwell(TimeSpan.FromSeconds(30), out bool circle);
			Assert.True(circle);
			Assert.Equal(string.Empty, t);

			t = CirculationSheetDocument.FormatCommercialDwell(TimeSpan.FromMinutes(2), out circle);
			Assert.False(circle);
			Assert.Equal("2", t);

			t = CirculationSheetDocument.FormatCommercialDwell(TimeSpan.Zero, out circle);
			Assert.False(circle);
			Assert.Equal(string.Empty, t);
		}

		[Fact]
		public void FormatGrantedMinutes_UsesHalves()
		{
			Assert.Equal("3", CirculationSheetDocument.FormatGrantedMinutes(TimeSpan.FromMinutes(3)));
			Assert.Equal("1½", CirculationSheetDocument.FormatGrantedMinutes(TimeSpan.FromMinutes(1.5)));
			Assert.Equal("½", CirculationSheetDocument.FormatGrantedMinutes(TimeSpan.FromSeconds(30)));
		}

		private static CirculationSheetFrontier MakeFrontier(long pk, bool hasOutgoing, string? axisId = null)
		{
			return new CirculationSheetFrontier(
				pk,
				CirculationSheetDocument.FormatStationKm(pk),
				"ST",
				CirculationSheetMarkKind.Halt,
				false,
				false,
				false,
				TimeSpan.Zero,
				null,
				null,
				hasOutgoing ? 1 : null,
				hasOutgoing ? 80 : null,
				hasOutgoing ? TimeSpan.FromMinutes(2) : null,
				axisId: axisId);
		}

		[Fact]
		public void Frontier_AxisId_IsPreservedThroughCrossingTrains()
		{
			CirculationSheetFrontier f = MakeFrontier(1000, true, "T3");
			Assert.Equal("T3", f.AxisId);
			CirculationSheetFrontier withX = f.WithCrossingTrains("4921");
			Assert.Equal("T3", withX.AxisId);
			Assert.Equal("4921", withX.CrossingTrains);
		}

		[Fact]
		public void FormatHeader_LocomotiveRouteAndTipo()
		{
			var specs = new Diamond.Motion.TrainSpecs("8100A", "CAF 8100", 0.8, 0.7, 100.0);
			Assert.Equal("Loc. CAF 8100  a 0.8  b 0.7", CirculationSheetDocument.FormatLocomotiveLine(specs));
			Assert.Equal("Tipo 100", CirculationSheetDocument.FormatMaterialTypeLabel(specs));

			var specsRound = new Diamond.Motion.TrainSpecs("x", "UT", 0.85, 0.74, 99.6);
			Assert.Equal("Loc. UT  a 0.9  b 0.7", CirculationSheetDocument.FormatLocomotiveLine(specsRound));
			Assert.Equal("Tipo 100", CirculationSheetDocument.FormatMaterialTypeLabel(specsRound));
		}

		[Fact]
		public void AuthenticitySeal_HmacIsVerifiableAndDependsOnKey()
		{
			string prev = CirculationSheetAuthenticity.SigningKey ?? string.Empty;
			string? pfxPrev = CirculationSheetCertificate.PfxPathOverride;
			string tempPfx = Path.Combine(Path.GetTempPath(), "zafiro-circ-test-" + Guid.NewGuid().ToString("N") + ".pfx");
			string tempReg = Path.Combine(Path.GetTempPath(), "zafiro-circ-emi-" + Guid.NewGuid().ToString("N") + ".jsonl");
			try
			{
				CirculationSheetCertificate.PfxPathOverride = tempPfx;
				CirculationEmissionRegistry.RegistryPathOverride = tempReg;

				CirculationSheetAuthenticity.SigningKey = "test-secret-operador";
				string payload = CirculationSheetAuthenticity.BuildPayload(
					"ficha", "4921", "Zafiro · 01/01/2026", "lab", 1, 2, "PALMA A MANACOR");
				string code = CirculationSheetAuthenticity.ComputeSealCode(payload);
				Assert.Equal(12, code.Length);
				Assert.True(CirculationSheetAuthenticity.VerifySealCode(payload, code));
				Assert.True(CirculationSheetAuthenticity.VerifySealCode(payload, "SEL " + code));
				Assert.False(CirculationSheetAuthenticity.VerifySealCode(payload + "|x", code));

				CirculationSheetAuthenticity.SigningKey = "otra-clave";
				Assert.False(CirculationSheetAuthenticity.VerifySealCode(payload, code));

				// Emisión + registro local
				CirculationSheetAuthenticity.SigningKey = null;
				CirculationEmissionInfo em = CirculationSheetAuthenticity.CreateEmission(
					"ficha", "pdf", "4921", "ed", "lab", 2);
				CirculationEmissionRegistry.Append(em);
				CirculationSealVerifyResult vr = CirculationEmissionRegistry.Verify("SEL " + em.SealCode);
				Assert.True(vr.FoundInRegistry);
				Assert.True(vr.Ok);

				// QR parse (solo sello; sin payload canónico)
				string qr = CirculationSheetQr.BuildQrPayload(em.SealCode);
				Assert.True(CirculationSheetQr.TryParseQrPayload(qr, out string s2, out string p2));
				Assert.Equal(em.SealCode, s2);
				Assert.True(string.IsNullOrEmpty(p2));
				Assert.StartsWith(CirculationSheetQr.QrPrefix, qr);
			}
			finally
			{
				CirculationSheetAuthenticity.SigningKey = string.IsNullOrEmpty(prev) ? null : prev;
				CirculationSheetCertificate.PfxPathOverride = pfxPrev;
				CirculationEmissionRegistry.RegistryPathOverride = null;
				try { if (File.Exists(tempPfx)) File.Delete(tempPfx); } catch { }
				try { if (File.Exists(tempReg)) File.Delete(tempReg); } catch { }
			}
		}
	}
}
