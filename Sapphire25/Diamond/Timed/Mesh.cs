using System.Collections.Generic;
using Diamond.Motion;
using Diamond.Topo;

namespace Diamond.Timed
{
	/// <summary>
	/// Salida del planificador: malla de circulaciones con asimilaciones factorizadas.
	/// </summary>
	public sealed class Mesh
	{
		private readonly List<Asimilation> mcolAsimilations;
		private readonly List<Circulation> mcolCirculations;
		private readonly List<string> mcolWarnings;
		private readonly List<string> mcolErrors;

		public Mesh()
		{
			mcolAsimilations = new List<Asimilation>();
			mcolCirculations = new List<Circulation>();
			mcolWarnings = new List<string>();
			mcolErrors = new List<string>();
		}

		/// <summary>
		/// Perfiles de marcha reutilizados (mínimo posible para el conjunto de circulaciones).
		/// </summary>
		public IReadOnlyList<Asimilation> Asimilations
		{
			get { return mcolAsimilations; }
		}

		public IReadOnlyList<Circulation> Circulations
		{
			get { return mcolCirculations; }
		}

		/// <summary>
		/// Desvíos blandos respecto a la demanda (cadencia, cobertura horaria, recuento…).
		/// </summary>
		public IReadOnlyList<string> Warnings
		{
			get { return mcolWarnings; }
		}

		/// <summary>
		/// Violaciones duras (acantonamiento, cruces en vía única).
		/// </summary>
		public IReadOnlyList<string> Errors
		{
			get { return mcolErrors; }
		}

		public bool Success
		{
			get { return mcolErrors.Count == 0; }
		}

		/// <summary>
		/// Rectángulos de ocupación de cantón (tiempo × espacio) de las circulaciones en <paramref name="axis"/>.
		/// Dos trenes son compatibles en este modelo si sus rectángulos no se superponen.
		/// </summary>
		public IReadOnlyList<CantonOccupationRect> GetCantonOccupations(Axis axis)
		{
			return MeshCantonGeometry.BuildOccupations(this, axis);
		}

		internal void AddAsimilation(Asimilation asimilation)
		{
			mcolAsimilations.Add(asimilation);
		}

		internal void AddCirculation(Circulation circulation)
		{
			mcolCirculations.Add(circulation);
		}

		internal void AddWarning(string message)
		{
			mcolWarnings.Add(message);
		}

		internal void AddError(string message)
		{
			mcolErrors.Add(message);
		}
	}
}
