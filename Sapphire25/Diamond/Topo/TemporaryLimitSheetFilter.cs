namespace Diamond.Topo
{
	/// <summary>
	/// Vía de limitación temporal que ve un tren en ficha / libro:
	/// ascendente (impar) → vía 1; descendente (par) en BAB → vía 2; descendente en BAU → vía 1.
	/// </summary>
	public static class TemporaryLimitSheetFilter
	{
		public static TemporaryLimitTrack TrackForTrain(bool ascending, int trackCount)
		{
			if (ascending || trackCount < 2)
			{
				return TemporaryLimitTrack.Track1;
			}

			return TemporaryLimitTrack.Track2;
		}

		public static bool Applies(TemporaryLimitTrack limitTrack, bool ascending, int trackCount)
		{
			return SpeedLimitFlattener.AppliesToTrack(
				limitTrack,
				TrackForTrain(ascending, trackCount));
		}

		public static bool Applies(TemporarySpeedLimit limit, bool ascending, int trackCount)
		{
			if (limit is null)
			{
				return false;
			}

			return Applies(limit.Track, ascending, trackCount);
		}

		public static bool ContainsPk(TemporarySpeedLimit limit, long pk)
		{
			if (limit is null)
			{
				return false;
			}

			long lo = limit.PK < limit.PKEnd ? limit.PK : limit.PKEnd;
			long hi = limit.PK > limit.PKEnd ? limit.PK : limit.PKEnd;
			return pk >= lo && pk < hi;
		}
	}
}
