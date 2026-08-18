using System;

namespace Diamond.Topo
{
	/// <summary>
	/// Limitación temporal de velocidad: misma geometría que una fija
	/// (<see cref="SpeedLimitSpan"/>) más vía, motivo, edición y señalización.
	/// No vive en el XML de topología: se almacena aparte, anclada a un topo.
	/// </summary>
	public class TemporarySpeedLimit : SpeedLimitSpan
	{
		private string mvarAxisId;
		private TemporaryLimitTrack mvarTrack;
		private bool mvarIsNewCreation;
		private TemporaryLimitReason mvarReason;
		private DateTime mvarCreatedAt;
		private bool mvarSignaledOnTrack;
		private string mvarObservations;

		public TemporarySpeedLimit()
			: base()
		{
			mvarAxisId = string.Empty;
			mvarTrack = TemporaryLimitTrack.Both;
			mvarIsNewCreation = true;
			mvarReason = TemporaryLimitReason.Other;
			mvarCreatedAt = DateTime.UtcNow;
			mvarSignaledOnTrack = true;
			mvarObservations = string.Empty;
		}

		public TemporarySpeedLimit(long pk0, long pkf, int speed)
			: base(pk0, pkf, speed)
		{
			mvarAxisId = string.Empty;
			mvarTrack = TemporaryLimitTrack.Both;
			mvarIsNewCreation = true;
			mvarReason = TemporaryLimitReason.Other;
			mvarCreatedAt = DateTime.UtcNow;
			mvarSignaledOnTrack = true;
			mvarObservations = string.Empty;
		}

		/// <summary>Identificador del eje de la topología (p. ej. T3).</summary>
		public string AxisId
		{
			get { return mvarAxisId; }
			set { mvarAxisId = value ?? string.Empty; }
		}

		public TemporaryLimitTrack Track
		{
			get { return mvarTrack; }
			set { mvarTrack = value; }
		}

		/// <summary>True si aparece desde la pasada edición del listado.</summary>
		public bool IsNewCreation
		{
			get { return mvarIsNewCreation; }
			set { mvarIsNewCreation = value; }
		}

		public TemporaryLimitReason Reason
		{
			get { return mvarReason; }
			set { mvarReason = value; }
		}

		public DateTime CreatedAt
		{
			get { return mvarCreatedAt; }
			set { mvarCreatedAt = value; }
		}

		/// <summary>True si la limitación está señalizada en vía.</summary>
		public bool SignaledOnTrack
		{
			get { return mvarSignaledOnTrack; }
			set { mvarSignaledOnTrack = value; }
		}

		/// <summary>Detalle opcional del motivo (sobre todo si es Otros).</summary>
		public string Observations
		{
			get { return mvarObservations; }
			set { mvarObservations = value ?? string.Empty; }
		}
	}
}
