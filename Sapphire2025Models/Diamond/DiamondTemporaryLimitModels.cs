namespace Sapphire2025Models.Diamond
{
	/// <summary>Vía a la que aplica una limitación temporal (1, 2 o ambas).</summary>
	public enum TemporaryLimitTrack : byte
	{
		Track1 = 1,
		Track2 = 2,
		Both = 3
	}

	/// <summary>Causa de una limitación temporal de velocidad.</summary>
	public enum TemporaryLimitReason : byte
	{
		Works = 0,
		Geometry = 1,
		TracksideHazard = 2,
		Electrification = 3,
		Gauge = 4,
		Weather = 5,
		NaturalDisaster = 6,
		Other = 7
	}

	public static class TemporaryLimitLabels
	{
		public static string TrackName(TemporaryLimitTrack track)
		{
			switch (track)
			{
				case TemporaryLimitTrack.Track1:
					return "Vía 1";
				case TemporaryLimitTrack.Track2:
					return "Vía 2";
				case TemporaryLimitTrack.Both:
					return "Ambas";
				default:
					return "—";
			}
		}

		public static string ReasonName(TemporaryLimitReason reason)
		{
			switch (reason)
			{
				case TemporaryLimitReason.Works:
					return "Obras";
				case TemporaryLimitReason.Geometry:
					return "Geometría";
				case TemporaryLimitReason.TracksideHazard:
					return "Peligro junto a la vía";
				case TemporaryLimitReason.Electrification:
					return "Electrificación";
				case TemporaryLimitReason.Gauge:
					return "Gálibo";
				case TemporaryLimitReason.Weather:
					return "Meteorología";
				case TemporaryLimitReason.NaturalDisaster:
					return "Catástrofe natural";
				case TemporaryLimitReason.Other:
					return "Otros";
				default:
					return "—";
			}
		}
	}

	/// <summary>Tramo de limitación ya resuelto (o una capa fija del XML).</summary>
	public class DiamondSpeedSpanModel
	{
		public long Pk0 { get; set; }

		public long Pkf { get; set; }

		public int Speed { get; set; }
	}

	/// <summary>Eje de una topología (para el editor de limitaciones).</summary>
	public class DiamondTopoAxisModel
	{
		public string Id { get; set; } = string.Empty;

		public string Name { get; set; } = string.Empty;

		public long Pk0 { get; set; }

		public long Pkf { get; set; }

		public int Vmax { get; set; }

		public int DefaultTrackCount { get; set; }

		/// <summary>Limitaciones fijas del XML, por capa (pueden solaparse entre velocidades).</summary>
		public List<DiamondSpeedSpanModel> FixedLimits { get; set; } = new List<DiamondSpeedSpanModel>();
	}

	/// <summary>Limitación temporal persistida, anclada a una topología y un eje.</summary>
	public class DiamondTemporaryLimitModel
	{
		public Guid Id { get; set; }

		public Guid TopoId { get; set; }

		public string AxisId { get; set; } = string.Empty;

		public long Pk0 { get; set; }

		public long Pkf { get; set; }

		public int Speed { get; set; }

		public TemporaryLimitTrack Track { get; set; } = TemporaryLimitTrack.Both;

		public bool IsNewCreation { get; set; }

		public TemporaryLimitReason Reason { get; set; } = TemporaryLimitReason.Other;

		public DateTime CreatedUtc { get; set; }

		public bool SignaledOnTrack { get; set; }

		public string Observations { get; set; } = string.Empty;
	}

	public class DiamondTemporaryLimitSaveRequest
	{
		public Guid? Id { get; set; }

		public Guid TopoId { get; set; }

		public string AxisId { get; set; } = string.Empty;

		public long Pk0 { get; set; }

		public long Pkf { get; set; }

		public int Speed { get; set; }

		public TemporaryLimitTrack Track { get; set; } = TemporaryLimitTrack.Both;

		public bool IsNewCreation { get; set; } = true;

		public TemporaryLimitReason Reason { get; set; } = TemporaryLimitReason.Other;

		public bool SignaledOnTrack { get; set; }

		public string Observations { get; set; } = string.Empty;
	}

	public class DiamondTemporaryLimitSaveResult
	{
		public bool Success { get; set; }

		public string Message { get; set; } = string.Empty;

		public DiamondTemporaryLimitModel? Item { get; set; }
	}
}
