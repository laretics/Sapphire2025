namespace Diamond.Controls.Rendering
{
	/// <summary>
	/// Paginación de fronteras de la ficha de marcha.
	/// Si el número de filas supera el máximo por página, se reparte de forma equitativa
	/// en <c>ceil(n/max)</c> páginas de modo que la media por página sea ≤ máximo.
	/// </summary>
	public static class CirculationSheetPager
	{
		/// <summary>
		/// Techo por página: deja margen bajo MinRowH≈13 px en A4 (≈50 filas caben;
		/// 36 deja filas legibles ~17 px y evita recortes al imprimir).
		/// </summary>
		public const int DefaultMaxFrontiersPerPage = 36;

		/// <summary>
		/// Máximo de filas que caben en una página A4 con la geometría del renderer
		/// (altura mínima de fila). El paginador no debería superar este valor.
		/// </summary>
		public static int MaxFrontiersFittingPage
		{
			get
			{
				// Debe coincidir con CirculationSheetSvgRenderer (márgenes + cabeceras + MinRowH).
				const double pageH = 841.89;
				const double marginT = 20;
				const double marginB = 34;
				const double headerBand = 22;
				const double subHeader = 15;
				const double colHeader = 17;
				const double footerPad = 16;
				const double minRowH = 13;
				double bodyTop = marginT + headerBand + subHeader + colHeader;
				double bodyBottom = pageH - marginB - footerPad;
				double avail = bodyBottom - bodyTop;
				int n = (int)Math.Floor(avail / minRowH);
				if (n < 8)
				{
					n = 8;
				}

				return n;
			}
		}

		/// <summary>
		/// Calcula el número de páginas necesarias para <paramref name="frontierCount"/> filas
		/// sin superar <paramref name="maxPerPage"/> de media (ni de techo por página).
		/// </summary>
		public static int ComputePageCount(int frontierCount, int maxPerPage)
		{
			if (frontierCount <= 0)
			{
				return 1;
			}

			maxPerPage = ClampMaxPerPage(maxPerPage);

			// Empezar en 1 e ir añadiendo páginas hasta media ≤ max.
			int pages = 1;
			while ((double)frontierCount / pages > maxPerPage)
			{
				pages++;
			}

			return pages;
		}

		private static int ClampMaxPerPage(int maxPerPage)
		{
			if (maxPerPage < 1)
			{
				maxPerPage = 1;
			}

			int fit = MaxFrontiersFittingPage;
			if (maxPerPage > fit)
			{
				maxPerPage = fit;
			}

			return maxPerPage;
		}

		/// <summary>
		/// Reparte las fronteras en páginas de tamaños lo más iguales posible.
		/// </summary>
		public static IReadOnlyList<CirculationSheetPage> Paginate(
			IReadOnlyList<CirculationSheetFrontier> frontiers,
			int maxPerPage = DefaultMaxFrontiersPerPage)
		{
			if (frontiers is null)
			{
				frontiers = Array.Empty<CirculationSheetFrontier>();
			}

			maxPerPage = ClampMaxPerPage(maxPerPage);

			int n = frontiers.Count;
			int pageCount = ComputePageCount(n, maxPerPage);
			List<CirculationSheetPage> pages = new List<CirculationSheetPage>(pageCount);

			if (n == 0)
			{
				pages.Add(new CirculationSheetPage(0, 1, Array.Empty<CirculationSheetFrontier>()));
				return pages;
			}

			// Tamaños equilibrados: las primeras (n % pageCount) páginas tienen una fila más.
			int baseSize = n / pageCount;
			int remainder = n % pageCount;
			int offset = 0;
			int p = 0;
			while (p < pageCount)
			{
				int size = baseSize + (p < remainder ? 1 : 0);
				if (size < 0)
				{
					size = 0;
				}

				if (offset + size > n)
				{
					size = n - offset;
				}

				List<CirculationSheetFrontier> slice = new List<CirculationSheetFrontier>(size);
				int i = 0;
				while (i < size)
				{
					slice.Add(frontiers[offset + i]);
					i++;
				}

				pages.Add(new CirculationSheetPage(p, pageCount, slice));
				offset += size;
				p++;
			}

			return pages;
		}
	}
}
