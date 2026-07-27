namespace Diamond.Topo
{
	/// <summary>
	/// Tramo de eje con un número de vías constante en [Pk0, Pkf).
	/// </summary>
	public readonly struct TrackSpan
	{
		private readonly long mvarPk0;
		private readonly long mvarPkf;
		private readonly int mvarTrackCount;

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

			mvarPk0 = pk0;
			mvarPkf = pkf;
			mvarTrackCount = trackCount;
		}

		public long Pk0
		{
			get { return mvarPk0; }
		}

		public long Pkf
		{
			get { return mvarPkf; }
		}

		/// <summary>
		/// Número de vías (1 = única, 2 = doble, …).
		/// </summary>
		public int TrackCount
		{
			get { return mvarTrackCount; }
		}
	}
}
