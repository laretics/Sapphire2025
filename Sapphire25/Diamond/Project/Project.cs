using System;
using System.Collections.Generic;

namespace Diamond.Project
{
	/// <summary>
	/// Contenedor del resultado compilado de una malla: asimilaciones factorizadas y circulaciones.
	/// Base para documentación, turnos de material, tracción, etc.
	/// </summary>
	public sealed class Project
	{
		private string mvarId;
		private string mvarName;
		private DayOfWeek? mvarPlanningDay;
		private DateTime mvarCompiledUtc;
		private string mvarSourceScript;
		private readonly List<Asimilation> mcolAsimilations;
		private readonly List<Circulation> mcolCirculations;
		private readonly List<string> mcolNotes;

		public Project()
		{
			mvarId = string.Empty;
			mvarName = string.Empty;
			mvarPlanningDay = null;
			mvarCompiledUtc = DateTime.UtcNow;
			mvarSourceScript = string.Empty;
			mcolAsimilations = new List<Asimilation>();
			mcolCirculations = new List<Circulation>();
			mcolNotes = new List<string>();
		}

		public string Id
		{
			get { return mvarId; }
			set { mvarId = value ?? string.Empty; }
		}

		public string Name
		{
			get { return mvarName; }
			set { mvarName = value ?? string.Empty; }
		}

		/// <summary>Día de la semana de la malla origen (si aplica).</summary>
		public DayOfWeek? PlanningDay
		{
			get { return mvarPlanningDay; }
			set { mvarPlanningDay = value; }
		}

		public DateTime CompiledUtc
		{
			get { return mvarCompiledUtc; }
			internal set { mvarCompiledUtc = value; }
		}

		/// <summary>Script de demanda que originó la malla (referencia, opcional).</summary>
		public string SourceScript
		{
			get { return mvarSourceScript; }
			set { mvarSourceScript = value ?? string.Empty; }
		}

		public IReadOnlyList<Asimilation> Asimilations
		{
			get { return mcolAsimilations; }
		}

		public IReadOnlyList<Circulation> Circulations
		{
			get { return mcolCirculations; }
		}

		/// <summary>Notas de compilación (warnings heredados, recuentos, etc.).</summary>
		public IReadOnlyList<string> Notes
		{
			get { return mcolNotes; }
		}

		internal void AddAsimilation(Asimilation asimilation)
		{
			if (asimilation is null)
			{
				throw new ArgumentNullException(nameof(asimilation));
			}

			mcolAsimilations.Add(asimilation);
		}

		internal void AddCirculation(Circulation circulation)
		{
			if (circulation is null)
			{
				throw new ArgumentNullException(nameof(circulation));
			}

			mcolCirculations.Add(circulation);
		}

		internal void AddNote(string note)
		{
			if (!string.IsNullOrWhiteSpace(note))
			{
				mcolNotes.Add(note);
			}
		}

		public override string ToString()
		{
			string day = mvarPlanningDay.HasValue ? mvarPlanningDay.Value.ToString() : "?";
			return (mvarName.Length > 0 ? mvarName : "Project")
				+ " [" + day + "] "
				+ mcolCirculations.Count.ToString() + " trenes / "
				+ mcolAsimilations.Count.ToString() + " asimilaciones";
		}
	}
}
