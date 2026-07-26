namespace Diamond.Topo
{
	/// <summary>
	/// Tramo de eje con un número de vías constante en [Pk0, Pkf).
	/// </summary>
	public readonly struct TrackSpan
	{
		public TrackSpan(long pk0, long pkf, int trackCount)
		{
			if (trackCount < 1)
			{
				throw new System.ArgumentOutOfRangeException(nameof(trackCount));
			}

			if (pkf < pk0)
			{
				long swap = pk0;
				pk0 = pkf;
				pkf = swap;
			}

			Pk0 = pk0;
			Pkf = pkf;
			TrackCount = trackCount;
		}

		public long Pk0 { get; }

		public long Pkf { get; }

		/// <summary>
		/// Número de vías (1 = única, 2 = doble, …).
		/// </summary>
		public int TrackCount { get; }
	}
}
