using System;
using System.Collections.Generic;

namespace Diamond.Topo
{
	/// <summary>
	/// Documento topográfico canónico: metadatos, catálogo de estaciones y ejes.
	/// </summary>
	public sealed class TopoLayout
	{
		private readonly LayoutInfo mvarInfo;
		private readonly List<Station> mcolStations;
		private readonly List<Axis> mcolAxes;

		public TopoLayout()
		{
			mvarInfo = new LayoutInfo();
			mcolStations = new List<Station>();
			mcolAxes = new List<Axis>();
		}

		public LayoutInfo Info
		{
			get { return mvarInfo; }
		}

		/// <summary>
		/// Catálogo de estaciones con identidad única.
		/// </summary>
		public IReadOnlyList<Station> Stations
		{
			get { return mcolStations; }
		}

		public IReadOnlyList<Axis> Axes
		{
			get { return mcolAxes; }
		}

		public void AddStation(Station station)
		{
			if (station is null)
			{
				throw new ArgumentNullException(nameof(station));
			}

			if (FindStationById(station.Id) is not null)
			{
				throw new InvalidOperationException($"Ya existe una estación con id '{station.Id}'.");
			}

			mcolStations.Add(station);
		}

		/// <summary>
		/// Obtiene la estación con el id dado o la crea y registra en el catálogo.
		/// </summary>
		public Station GetOrAddStation(string id)
		{
			if (string.IsNullOrEmpty(id))
			{
				throw new ArgumentException("El id de estación no puede ser vacío.", nameof(id));
			}

			Station? existing = FindStationById(id);
			if (existing is not null)
			{
				return existing;
			}

			Station created = new Station(id);
			mcolStations.Add(created);
			return created;
		}

		public Station? FindStationById(string id)
		{
			if (id is null)
			{
				return null;
			}

			int index = 0;
			while (index < mcolStations.Count)
			{
				if (string.Equals(mcolStations[index].Id, id, StringComparison.Ordinal))
				{
					return mcolStations[index];
				}

				index++;
			}

			return null;
		}

		public void ClearStations()
		{
			mcolStations.Clear();
		}

		public void AddAxis(Axis axis)
		{
			if (axis is null)
			{
				throw new ArgumentNullException(nameof(axis));
			}

			mcolAxes.Add(axis);
		}

		public void ClearAxes()
		{
			mcolAxes.Clear();
		}

		public Axis? FindAxisById(string id)
		{
			if (id is null)
			{
				return null;
			}

			int index = 0;
			while (index < mcolAxes.Count)
			{
				if (string.Equals(mcolAxes[index].Id, id, StringComparison.Ordinal))
				{
					return mcolAxes[index];
				}

				index++;
			}

			return null;
		}

		/// <summary>
		/// Recalcula PK, índices espaciales e incidencias de estación de todos los ejes.
		/// </summary>
		public void RebuildAll()
		{
			int index = 0;
			while (index < mcolAxes.Count)
			{
				mcolAxes[index].Rebuild();
				index++;
			}
		}
	}
}
