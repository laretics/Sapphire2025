using System;
using System.Collections.Generic;

namespace Diamond.Topo
{
	/// <summary>
	/// Aplica limitaciones temporales del almacén sobre una topología en memoria.
	/// No forman parte del XML: se vuelcan en <see cref="Axis.TemporaryLimits"/>.
	/// </summary>
	public static class TopoTemporaryLimits
	{
		public static void Clear(TopoLayout layout)
		{
			if (layout is null)
			{
				throw new ArgumentNullException(nameof(layout));
			}

			int i = 0;
			while (i < layout.Axes.Count)
			{
				layout.Axes[i].TemporaryLimits.Clear();
				layout.Axes[i].ClearTemporaryRecords();
				i++;
			}
		}

		public static void Apply(TopoLayout layout, IReadOnlyList<TemporarySpeedLimit> limits)
		{
			if (layout is null)
			{
				throw new ArgumentNullException(nameof(layout));
			}

			Clear(layout);
			if (limits is null || limits.Count == 0)
			{
				return;
			}

			int i = 0;
			while (i < limits.Count)
			{
				TemporarySpeedLimit limit = limits[i];
				if (limit is null || string.IsNullOrWhiteSpace(limit.AxisId))
				{
					i++;
					continue;
				}

				Axis? axis = layout.FindAxisById(limit.AxisId);
				if (axis is not null)
				{
					axis.TemporaryLimits.Add(limit.Speed, limit.PK, limit.PKEnd);
					axis.AddTemporaryRecord(limit);
				}

				i++;
			}
		}

		public static TemporarySpeedLimit FromSpan(
			string axisId,
			long pk0,
			long pkf,
			int speed,
			TemporaryLimitReason reason = TemporaryLimitReason.Other,
			string? observations = null,
			TemporaryLimitTrack track = TemporaryLimitTrack.Both,
			bool isNewCreation = false,
			DateTime? createdAt = null,
			bool signaledOnTrack = true)
		{
			TemporarySpeedLimit limit = new TemporarySpeedLimit(pk0, pkf, speed);
			limit.AxisId = axisId ?? string.Empty;
			limit.Reason = reason;
			limit.Observations = observations ?? string.Empty;
			limit.Track = track;
			limit.IsNewCreation = isNewCreation;
			limit.SignaledOnTrack = signaledOnTrack;
			if (createdAt.HasValue)
			{
				limit.CreatedAt = createdAt.Value;
			}

			return limit;
		}
	}
}
