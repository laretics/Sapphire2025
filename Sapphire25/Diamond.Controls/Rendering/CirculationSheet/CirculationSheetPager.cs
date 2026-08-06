namespace Diamond.Controls.Rendering
{
	/// <summary>
	/// Paginación del libro itinerario: reparte fronteras en páginas de libro
	/// (mitades) de forma equilibrada, sin superar el máximo por mitad.
	/// Cada 2 páginas de libro forman una hoja A4 apaisada en el renderer.
	/// </summary>
	public static class CirculationSheetPager
	{
		/// <summary>
		/// Techo por mitad de libro: filas legibles al estirar en A4 apaisado.
		/// </summary>
		public const int DefaultMaxFrontiersPerPage = 30;

		/// <summary>
		/// Máximo de filas que caben en una mitad con altura mínima de fila.
		/// </summary>
		public static int MaxFrontiersFittingPage
		{
			get { return CirculationSheetSvgRenderer.MaxRowsPerBookPage; }
		}

		/// <summary>
		/// Número de páginas de libro (mitades) necesarias.
		/// </summary>
		public static int ComputePageCount(int frontierCount, int maxPerPage)
		{
			if (frontierCount <= 0)
			{
				return 1;
			}

			maxPerPage = ClampMaxPerPage(maxPerPage);

			int pages = 1;
			while ((double)frontierCount / pages > maxPerPage)
			{
				pages++;
			}

			return pages;
		}

		/// <summary>Número de hojas físicas A4 apaisadas (2 mitades por hoja).</summary>
		public static int ComputeSheetCount(int bookPageCount)
		{
			if (bookPageCount <= 0)
			{
				return 1;
			}

			return (bookPageCount + 1) / 2;
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
		/// Reparte las fronteras en páginas de libro de tamaños lo más iguales posible
		/// (misma cantidad de filas ±1), para estirar cada tabla a la misma altura de hoja.
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
