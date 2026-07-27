namespace Diamond.Controls.Rendering
{
	/// <summary>Una página de la ficha (subconjunto de fronteras + metadatos de impresión).</summary>
	public sealed class CirculationSheetPage
	{
		private readonly int mvarPageIndex;
		private readonly int mvarPageCount;
		private readonly IReadOnlyList<CirculationSheetFrontier> mcolFrontiers;

		public CirculationSheetPage(
			int pageIndex,
			int pageCount,
			IReadOnlyList<CirculationSheetFrontier> frontiers)
		{
			mvarPageIndex = pageIndex;
			mvarPageCount = pageCount;
			mcolFrontiers = frontiers ?? Array.Empty<CirculationSheetFrontier>();
		}

		/// <summary>Índice 0-based.</summary>
		public int PageIndex
		{
			get { return mvarPageIndex; }
		}

		public int PageNumber
		{
			get { return mvarPageIndex + 1; }
		}

		public int PageCount
		{
			get { return mvarPageCount; }
		}

		public IReadOnlyList<CirculationSheetFrontier> Frontiers
		{
			get { return mcolFrontiers; }
		}
	}
}
