namespace Tourmaline26.Services.Correspondence
{
	public enum ConnectionMode
	{
		Train = 0,
		Bus = 1,
		Emt = 2
	}

	/// <summary>Fila de enlace (tren SFM, bus TIB o bus EMT) para el panel de correspondencias.</summary>
	public sealed class ConnectionDeparture : ISortable, IComparable<ConnectionDeparture>
	{
		public ConnectionMode Mode { get; init; }
		public bool IsBus => Mode is ConnectionMode.Bus or ConnectionMode.Emt;
		public DateTime DepartureTimeLocal { get; init; }
		public DateTime EstimatedTimeLocal { get; init; }
		public string LineSymbol { get; init; } = string.Empty;
		public string LineColorHex { get; init; } = "#888888";
		public string DestinationName { get; init; } = string.Empty;
		public string ServiceName { get; init; } = string.Empty;
		public long TripId { get; init; }
		public int? Platform { get; init; }
		public int? OriginalPlatform { get; init; }
		public bool PlatformChanged { get; init; }
		public string? Notice { get; init; }

		public DateTime SortTime
		{
			get
			{
				if (EstimatedTimeLocal != default && EstimatedTimeLocal != DateTime.MinValue)
					return EstimatedTimeLocal;
				return DepartureTimeLocal;
			}
		}

		public int CompareTo(ConnectionDeparture? other)
		{
			if (other is null)
				return 1;
			int byTime = SortTime.CompareTo(other.SortTime);
			if (byTime != 0)
				return byTime;
			int byLine = string.Compare(LineSymbol, other.LineSymbol, StringComparison.OrdinalIgnoreCase);
			if (byLine != 0)
				return byLine;
			return string.Compare(DestinationName, other.DestinationName, StringComparison.CurrentCultureIgnoreCase);
		}
	}
}
