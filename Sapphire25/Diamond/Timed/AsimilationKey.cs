using System;
using System.Collections.Generic;
using System.Text;
using Diamond.Motion;
using Diamond.Topo;

namespace Diamond.Timed
{
	/// <summary>
	/// Clave canónica para compartir <see cref="Asimilation"/> entre circulaciones.
	/// </summary>
	internal sealed class AsimilationKey : IEquatable<AsimilationKey>
	{
		private readonly string mvarText;

		public AsimilationKey(
			string fleetId,
			string axisId,
			long originPk,
			long destinationPk,
			IReadOnlyList<AsimilationStop> stops)
		{
			StringBuilder builder = new StringBuilder();
			builder.Append(fleetId ?? string.Empty);
			builder.Append('|');
			builder.Append(axisId ?? string.Empty);
			builder.Append('|');
			builder.Append(originPk);
			builder.Append('>');
			builder.Append(destinationPk);

			int index = 0;
			while (index < stops.Count)
			{
				AsimilationStop stop = stops[index];
				builder.Append(';');
				builder.Append(stop.PK);
				builder.Append('@');
				builder.Append((long)stop.Dwell.TotalSeconds);
				index++;
			}

			mvarText = builder.ToString();
		}

		public bool Equals(AsimilationKey? other)
		{
			if (other is null)
			{
				return false;
			}

			return string.Equals(mvarText, other.mvarText, StringComparison.Ordinal);
		}

		public override bool Equals(object? obj)
		{
			return Equals(obj as AsimilationKey);
		}

		public override int GetHashCode()
		{
			return mvarText.GetHashCode(StringComparison.Ordinal);
		}

		public override string ToString()
		{
			return mvarText;
		}
	}
}
