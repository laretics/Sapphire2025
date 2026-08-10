namespace Diamond.Controls.Rendering
{
	/// <summary>
	/// Paginación del libro itinerario: reparte fronteras en páginas de libro
	/// (mitades) de forma equilibrada.
	/// En la 2.ª página y siguientes se repite como primer registro el último de la
	/// página anterior (continuidad al pasar de hoja). La 1.ª hoja admite menos filas
	/// porque la subcabecera (Loc./ruta) reduce el alto del cuerpo.
	/// Cada 2 páginas de libro forman una hoja A4 apaisada en el renderer.
	/// </summary>
	public static class CirculationSheetPager
	{
		/// <summary>
		/// Techo por defecto por mitad (se acota además por la geometría A4).
		/// </summary>
		public const int DefaultMaxFrontiersPerPage = 30;

		/// <summary>
		/// Máximo de filas en la 1.ª hoja del tren (geometría con subcabecera).
		/// </summary>
		public static int MaxFrontiersFittingPage
		{
			get { return CirculationSheetSvgRenderer.MaxRowsPerBookPage; }
		}

		/// <summary>Máximo de filas en una hoja de continuación (sin subcabecera Loc./ruta).</summary>
		public static int MaxFrontiersFittingContinuationPage
		{
			get { return CirculationSheetSvgRenderer.MaxRowsOnTrainPage(firstPageOfTrain: false); }
		}

		/// <summary>
		/// Número de páginas de libro necesarias con solape de 1 frontera y techos
		/// distintos para la 1.ª hoja y las de continuación.
		/// </summary>
		public static int ComputePageCount(int frontierCount, int maxPerPage)
		{
			ResolveCaps(maxPerPage, out int maxFirst, out int maxCont);
			return ComputePageCountCore(frontierCount, maxFirst, maxCont);
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

		/// <summary>
		/// Reparte las fronteras de forma equilibrada entre páginas.
		/// No llena la 1.ª al máximo dejando un resto corto: los tamaños de fila
		/// (con solape) difieren en a lo sumo 1, respetando el techo de cada tipo de hoja.
		/// </summary>
		public static IReadOnlyList<CirculationSheetPage> Paginate(
			IReadOnlyList<CirculationSheetFrontier> frontiers,
			int maxPerPage = DefaultMaxFrontiersPerPage)
		{
			if (frontiers is null)
			{
				frontiers = Array.Empty<CirculationSheetFrontier>();
			}

			ResolveCaps(maxPerPage, out int maxFirst, out int maxCont);

			int n = frontiers.Count;
			if (n == 0)
			{
				return new[] { new CirculationSheetPage(0, 1, Array.Empty<CirculationSheetFrontier>()) };
			}

			if (n <= maxFirst)
			{
				return new[] { new CirculationSheetPage(0, 1, CopyRange(frontiers, 0, n)) };
			}

			int pageCount = ComputePageCountCore(n, maxFirst, maxCont);
			int[]? sizes = BuildBalancedDisplaySizes(n, pageCount, maxFirst, maxCont);

			// Si el reparto equilibrado no cupo (no debería), reintentar subiendo páginas.
			while (sizes is null && pageCount < n)
			{
				pageCount++;
				sizes = BuildBalancedDisplaySizes(n, pageCount, maxFirst, maxCont);
			}

			if (sizes is null)
			{
				// Fallback codicioso (no debería alcanzarse).
				return PaginateGreedy(frontiers, maxFirst, maxCont);
			}

			List<CirculationSheetPage> pages = new List<CirculationSheetPage>(pageCount);
			// offset = índice de la primera frontera aún no cerrada (siguiente página
			// empezará en offset-1 por el solape).
			int offset = 0;
			int p = 0;
			while (p < pageCount)
			{
				int size = sizes[p];
				int startIndex;
				if (p == 0)
				{
					startIndex = 0;
					offset = size;
				}
				else
				{
					startIndex = offset - 1;
					if (startIndex < 0)
					{
						startIndex = 0;
					}

					// Nuevas fronteras en esta página = size - 1 (la 1.ª es solape).
					int newly = size - 1;
					if (newly < 1)
					{
						newly = 1;
					}

					offset += newly;
				}

				if (startIndex + size > n)
				{
					size = n - startIndex;
				}

				if (size < 1)
				{
					break;
				}

				pages.Add(new CirculationSheetPage(p, pageCount, CopyRange(frontiers, startIndex, size)));
				p++;
			}

			// Ajustar PageCount si el bucle se cortó.
			int actual = pages.Count;
			if (actual != pageCount && actual > 0)
			{
				List<CirculationSheetPage> fixedPages = new List<CirculationSheetPage>(actual);
				int i = 0;
				while (i < actual)
				{
					fixedPages.Add(new CirculationSheetPage(i, actual, pages[i].Frontiers));
					i++;
				}

				return fixedPages;
			}

			return pages;
		}

		private static void ResolveCaps(int maxPerPage, out int maxFirst, out int maxCont)
		{
			if (maxPerPage < 1)
			{
				maxPerPage = 1;
			}

			int fitFirst = MaxFrontiersFittingPage;
			int fitCont = MaxFrontiersFittingContinuationPage;
			if (fitCont < fitFirst)
			{
				fitCont = fitFirst;
			}

			maxFirst = maxPerPage < fitFirst ? maxPerPage : fitFirst;
			maxCont = maxPerPage < fitCont ? maxPerPage : fitCont;
			if (maxFirst < 1)
			{
				maxFirst = 1;
			}

			if (maxCont < 1)
			{
				maxCont = 1;
			}
		}

		private static int ComputePageCountCore(int frontierCount, int maxFirst, int maxCont)
		{
			if (frontierCount <= 0)
			{
				return 1;
			}

			if (frontierCount <= maxFirst)
			{
				return 1;
			}

			if (maxCont < 2)
			{
				// Sin solape útil: cada página 1 frontera (salvo la 1.ª).
				int rest = frontierCount - maxFirst;
				return 1 + rest;
			}

			// Capacidad máxima de fronteras únicas con P páginas:
			// maxFirst + (P-1)*(maxCont-1). Buscar P mínimo.
			int pages = 2;
			while (true)
			{
				int capacity = maxFirst + (pages - 1) * (maxCont - 1);
				if (capacity >= frontierCount)
				{
					return pages;
				}

				pages++;
				if (pages > frontierCount)
				{
					return frontierCount;
				}
			}
		}

		/// <summary>
		/// Tamaños de visualización (filas por página) lo más iguales posible,
		/// con sum(sizes) = n + (P-1) (solapes) y sizes[i] acotado por el techo de esa hoja.
		/// </summary>
		private static int[]? BuildBalancedDisplaySizes(int n, int pageCount, int maxFirst, int maxCont)
		{
			if (pageCount < 1 || n < 1)
			{
				return null;
			}

			if (pageCount == 1)
			{
				if (n > maxFirst)
				{
					return null;
				}

				return new[] { n };
			}

			// total de “slots” de fila dibujados = únicas + un solape por página extra.
			int totalSlots = n + (pageCount - 1);
			int[] sizes = new int[pageCount];
			int baseSize = totalSlots / pageCount;
			int rem = totalSlots % pageCount;
			int i = 0;
			while (i < pageCount)
			{
				// Reparte el resto entre las primeras páginas (diferencia ≤ 1).
				sizes[i] = baseSize + (i < rem ? 1 : 0);
				i++;
			}

			// Empujar excesos de techo hacia páginas con holgura.
			if (!RedistributeToCaps(sizes, maxFirst, maxCont))
			{
				return null;
			}

			// Comprobar mínimos: continuación con al menos 2 (solape + 1 nueva) si maxCont ≥ 2.
			i = 0;
			while (i < pageCount)
			{
				int min = i == 0 ? 1 : (maxCont >= 2 ? 2 : 1);
				if (sizes[i] < min)
				{
					return null;
				}

				int cap = i == 0 ? maxFirst : maxCont;
				if (sizes[i] > cap)
				{
					return null;
				}

				i++;
			}

			// Verificar cobertura única.
			int unique = sizes[0];
			i = 1;
			while (i < pageCount)
			{
				unique += sizes[i] - 1;
				i++;
			}

			if (unique != n)
			{
				return null;
			}

			return sizes;
		}

		/// <summary>
		/// Si alguna página supera su techo, mueve el exceso a otras con margen.
		/// Mantiene la suma total de slots.
		/// </summary>
		private static bool RedistributeToCaps(int[] sizes, int maxFirst, int maxCont)
		{
			int pageCount = sizes.Length;
			int guard = 0;
			while (guard < pageCount * pageCount + 8)
			{
				guard++;
				int over = -1;
				int i = 0;
				while (i < pageCount)
				{
					int cap = i == 0 ? maxFirst : maxCont;
					if (sizes[i] > cap)
					{
						over = i;
						break;
					}

					i++;
				}

				if (over < 0)
				{
					return true;
				}

				int capOver = over == 0 ? maxFirst : maxCont;
				int excess = sizes[over] - capOver;
				sizes[over] = capOver;

				// Dar el exceso a páginas por debajo de su techo (preferir equilibrar).
				int left = excess;
				int pass = 0;
				while (left > 0 && pass < pageCount * 2)
				{
					pass++;
					bool progressed = false;
					int j = 0;
					while (j < pageCount && left > 0)
					{
						if (j == over)
						{
							j++;
							continue;
						}

						int capJ = j == 0 ? maxFirst : maxCont;
						if (sizes[j] < capJ)
						{
							sizes[j]++;
							left--;
							progressed = true;
						}

						j++;
					}

					if (!progressed)
					{
						// No hay dónde poner el exceso.
						return false;
					}
				}

				if (left > 0)
				{
					return false;
				}
			}

			return false;
		}

		private static List<CirculationSheetFrontier> CopyRange(
			IReadOnlyList<CirculationSheetFrontier> frontiers,
			int startIndex,
			int size)
		{
			List<CirculationSheetFrontier> slice = new List<CirculationSheetFrontier>(size);
			int i = 0;
			while (i < size)
			{
				slice.Add(frontiers[startIndex + i]);
				i++;
			}

			return slice;
		}

		/// <summary>Reserva codiciosa solo si falla el reparto equilibrado.</summary>
		private static IReadOnlyList<CirculationSheetPage> PaginateGreedy(
			IReadOnlyList<CirculationSheetFrontier> frontiers,
			int maxFirst,
			int maxCont)
		{
			int n = frontiers.Count;
			List<int> starts = new List<int>();
			List<int> sizes = new List<int>();
			int offset = 0;
			while (offset < n)
			{
				bool first = starts.Count == 0;
				int cap = first ? maxFirst : maxCont;
				int startIndex;
				int size;
				if (first || cap < 2)
				{
					startIndex = offset;
					size = n - offset;
					if (size > cap)
					{
						size = cap;
					}

					offset += size;
				}
				else
				{
					startIndex = offset - 1;
					size = n - startIndex;
					if (size > cap)
					{
						size = cap;
					}

					offset += size - 1;
				}

				if (size < 1)
				{
					break;
				}

				starts.Add(startIndex);
				sizes.Add(size);
			}

			int pageCount = starts.Count;
			List<CirculationSheetPage> pages = new List<CirculationSheetPage>(pageCount);
			int p = 0;
			while (p < pageCount)
			{
				pages.Add(new CirculationSheetPage(p, pageCount, CopyRange(frontiers, starts[p], sizes[p])));
				p++;
			}

			return pages;
		}
	}
}
