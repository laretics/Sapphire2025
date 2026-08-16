using System;
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
		private DayOfWeek? mvarPlanningDay;

		public Mesh()
		{
			mcolAsimilations = new List<Asimilation>();
			mcolCirculations = new List<Circulation>();
			mcolWarnings = new List<string>();
			mcolErrors = new List<string>();
			mvarPlanningDay = null;
		}

		/// <summary>
		/// Día de la semana para el que se ha resuelto esta malla (si aplica).
		/// </summary>
		public DayOfWeek? PlanningDay
		{
			get { return mvarPlanningDay; }
			internal set { mvarPlanningDay = value; }
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
		/// Rectángulos de ocupación de cantón (tiempo × espacio) de las circulaciones en <paramref name="view"/>.
		/// Dos trenes son compatibles en este modelo si sus rectángulos no se superponen.
		/// </summary>
		public IReadOnlyList<CantonOccupationRect> GetCantonOccupations(RouteView view)
		{
			return MeshCantonGeometry.BuildOccupations(this, view);
		}

		/// <summary>
		/// Atajo mono-eje: envuelve el eje en <see cref="RouteView.FromAxis"/>.
		/// </summary>
		public IReadOnlyList<CantonOccupationRect> GetCantonOccupations(Axis axis)
		{
			return MeshCantonGeometry.BuildOccupations(this, axis);
		}

		/// <summary>
		/// Conflictos duros (intersecciones de ocupaciones incompatibles) en la vista.
		/// </summary>
		public IReadOnlyList<OccupationConflict> GetHardConflicts(RouteView view)
		{
			return MeshCantonGeometry.FindHardConflicts(this, view);
		}

		internal void AddAsimilation(Asimilation asimilation)
		{
			mcolAsimilations.Add(asimilation);
		}

		internal void AddCirculation(Circulation circulation)
		{
			mcolCirculations.Add(circulation);
		}

		/// <summary>
		/// Malla de solo lectura a partir de circulaciones ya materializadas
		/// (p. ej. hidratadas desde un plan publicado para calcular cruces).
		/// </summary>
		public static Mesh FromCirculations(
			IReadOnlyList<Circulation> circulations,
			DayOfWeek? planningDay = null)
		{
			Mesh mesh = new Mesh();
			mesh.PlanningDay = planningDay;
			if (circulations is null)
			{
				return mesh;
			}

			int i = 0;
			while (i < circulations.Count)
			{
				Circulation c = circulations[i];
				if (c is not null)
				{
					mesh.AddCirculation(c);
					if (c.Asimilation is not null && !mesh.mcolAsimilations.Contains(c.Asimilation))
					{
						mesh.AddAsimilation(c.Asimilation);
					}
				}

				i++;
			}

			return mesh;
		}

		/// <summary>
		/// Quita una circulación ya añadida (p. ej. tras <c>delete</c> del script).
		/// </summary>
		internal bool RemoveCirculation(Circulation circulation)
		{
			if (circulation is null)
			{
				return false;
			}

			return mcolCirculations.Remove(circulation);
		}

		internal void AddWarning(string message)
		{
			mcolWarnings.Add(message);
		}

		internal void AddError(string message)
		{
			mcolErrors.Add(message);
		}

		/// <summary>
		/// Reescribe errores y warnings (p. ej. para sustituir ids técnicos por números de tren).
		/// </summary>
		internal void RewriteMessages(System.Func<string, string> rewriter)
		{
			if (rewriter is null)
			{
				throw new ArgumentNullException(nameof(rewriter));
			}

			int i = 0;
			while (i < mcolErrors.Count)
			{
				mcolErrors[i] = rewriter(mcolErrors[i]) ?? mcolErrors[i];
				i++;
			}

			i = 0;
			while (i < mcolWarnings.Count)
			{
				mcolWarnings[i] = rewriter(mcolWarnings[i]) ?? mcolWarnings[i];
				i++;
			}
		}
	}
}
