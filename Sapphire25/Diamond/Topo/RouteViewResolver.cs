using System;
using System.Collections.Generic;

namespace Diamond.Topo
{
	/// <summary>
	/// Resuelve un <see cref="RouteView"/> a partir de un ViewId de asimilación
	/// (p. ej. "T3", "T3+T2") o del eje más cercano.
	/// </summary>
	public static class RouteViewResolver
	{
		/// <summary>
		/// Interpreta ViewId con ejes concatenados por '+', '|' o ','.
		/// Cada tramo usa el eje completo (PK … PKEnd).
		/// </summary>
		public static RouteView? TryFromViewId(TopoLayout topo, string? viewId)
		{
			if (topo is null || string.IsNullOrWhiteSpace(viewId))
			{
				return null;
			}

			string[] parts = viewId.Split(
				new[] { '+', '|', ',', ';' },
				StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			if (parts.Length == 0)
			{
				return null;
			}

			if (parts.Length == 1)
			{
				Axis? single = topo.FindAxisById(parts[0]);
				if (single is null)
				{
					return null;
				}

				return RouteView.FromAxis(single);
			}

			List<(Axis Axis, long FromPk, long ToPk)> segments =
				new List<(Axis, long, long)>();
			int i = 0;
			while (i < parts.Length)
			{
				Axis? axis = topo.FindAxisById(parts[i]);
				if (axis is null)
				{
					return null;
				}

				long from = axis.PK;
				long to = axis.PKEnd;
				if (to < from)
				{
					long swap = from;
					from = to;
					to = swap;
				}

				if (to == from)
				{
					return null;
				}

				segments.Add((axis, from, to));
				i++;
			}

			return RouteView.Concat(viewId.Trim(), viewId.Trim(), segments);
		}

		/// <summary>
		/// Vista de un eje concreto, o null.
		/// </summary>
		public static RouteView? TryFromAxis(Axis? axis)
		{
			if (axis is null)
			{
				return null;
			}

			try
			{
				return RouteView.FromAxis(axis);
			}
			catch (InvalidOperationException)
			{
				return null;
			}
		}

		/// <summary>
		/// Primer eje usable del layout.
		/// </summary>
		public static RouteView? TryFirstAxis(TopoLayout? topo)
		{
			if (topo is null || topo.Axes.Count == 0)
			{
				return null;
			}

			int i = 0;
			while (i < topo.Axes.Count)
			{
				RouteView? view = TryFromAxis(topo.Axes[i]);
				if (view is not null)
				{
					return view;
				}

				i++;
			}

			return null;
		}
	}
}
