using System;
using System.Collections.Generic;
using Diamond.Topo;

namespace Diamond.Cabin
{
	/// <summary>
	/// Ubicación lineal del tren sobre la topología (GPS, odómetro o entrada manual).
	/// </summary>
	public sealed class LinearLocation
	{
		public Axis? Axis { get; private set; }

		/// <summary>
		/// Si no es null, el GPS se restringe a estos ejes (ruta de la misión).
		/// </summary>
		public IReadOnlyList<Axis>? MissionAxes { get; set; }

		public long PKRef { get; private set; } = -1;

		public LinearLocationSource Source { get; private set; }

		public DateTime LastManualInput { get; private set; } = DateTime.MinValue;

		public DateTime LastOdometerUpdate { get; private set; } = DateTime.MinValue;

		public DateTime LastSatelliteInput { get; private set; } = DateTime.MinValue;

		public double LastProjectionDistanceMeters { get; private set; } = double.PositiveInfinity;

		/// <summary>
		/// Proyecta un punto WGS84 sobre la topología. Si hay <see cref="MissionAxes"/>, solo esos ejes.
		/// </summary>
		public bool TryLocateBySatellite(
			TopoLayout? layout,
			double latitude,
			double longitude,
			double rangeMeters = 1000.0)
		{
			if (layout is null)
			{
				return false;
			}

			IReadOnlyList<Axis> candidates;
			if (MissionAxes is not null && MissionAxes.Count > 0)
			{
				candidates = MissionAxes;
			}
			else
			{
				candidates = layout.Axes;
			}

			Axis? bestAxis = null;
			AxisProjection best = AxisProjection.Fail(double.PositiveInfinity);
			int i = 0;
			while (i < candidates.Count)
			{
				Axis axis = candidates[i];
				if (axis is null)
				{
					i++;
					continue;
				}

				AxisProjection proj = axis.PKFromLocation(latitude, longitude, rangeMeters);
				if (proj.Success && proj.DistanceMeters < best.DistanceMeters)
				{
					best = proj;
					bestAxis = axis;
				}

				i++;
			}

			if (bestAxis is null || !best.Success)
			{
				return false;
			}

			Axis = bestAxis;
			PKRef = best.PK;
			LastProjectionDistanceMeters = best.DistanceMeters;
			Source = LinearLocationSource.Satellite;
			LastSatelliteInput = DateTime.Now;
			return true;
		}

		public void SetManual(Axis axis, long pk)
		{
			if (axis is null)
			{
				throw new ArgumentNullException(nameof(axis));
			}

			Axis = axis;
			PKRef = pk;
			Source = LinearLocationSource.Manual;
			LastManualInput = DateTime.Now;
			LastProjectionDistanceMeters = 0.0;
		}

		public void Clear()
		{
			Axis = null;
			PKRef = -1;
			Source = LinearLocationSource.None;
			LastProjectionDistanceMeters = double.PositiveInfinity;
			MissionAxes = null;
		}
	}

	public enum LinearLocationSource
	{
		None = 0,
		Manual = 1,
		Odometer = 2,
		Satellite = 3
	}
}
