using System.Collections.Generic;
using Diamond.Motion;

namespace Diamond.Timed
{
	/// <summary>
	/// Resultado determinista de compilar un script de demanda.
	/// </summary>
	public sealed class DemandCompileResult
	{
		private readonly List<DemandRequirement> mcolRequirements;
		private readonly List<DemandDeleteOp> mcolDeletes;
		private readonly List<DemandAsimilationDef> mcolAsimilationDefs;
		private readonly List<DemandTopoConstraint> mcolTopoConstraints;
		private readonly List<TrainSpecs> mcolFleet;
		private readonly List<string> mcolErrors;
		private string mvarPlanName;
		private string mvarIncludedTopoPath;

		public DemandCompileResult()
		{
			mcolRequirements = new List<DemandRequirement>();
			mcolDeletes = new List<DemandDeleteOp>();
			mcolAsimilationDefs = new List<DemandAsimilationDef>();
			mcolTopoConstraints = new List<DemandTopoConstraint>();
			mcolFleet = new List<TrainSpecs>();
			mcolErrors = new List<string>();
			mvarPlanName = string.Empty;
			mvarIncludedTopoPath = string.Empty;
		}

		public string PlanName
		{
			get { return mvarPlanName; }
			internal set { mvarPlanName = value ?? string.Empty; }
		}

		/// <summary>
		/// Ruta del XML de topología declarada con <c>include</c> (tal cual en el script).
		/// Vacía si el script no incluye topología.
		/// </summary>
		public string IncludedTopoPath
		{
			get { return mvarIncludedTopoPath; }
			internal set { mvarIncludedTopoPath = value ?? string.Empty; }
		}

		public IReadOnlyList<DemandRequirement> Requirements
		{
			get { return mcolRequirements; }
		}

		/// <summary>
		/// Directivas <c>delete</c> en orden de aparición (también tienen <see cref="DemandDeleteOp.ScriptOrder"/>).
		/// </summary>
		public IReadOnlyList<DemandDeleteOp> Deletes
		{
			get { return mcolDeletes; }
		}

		/// <summary>
		/// Definiciones <c>asim</c> (serie de numeración y color por OD / días), en orden de script.
		/// </summary>
		public IReadOnlyList<DemandAsimilationDef> AsimilationDefs
		{
			get { return mcolAsimilationDefs; }
		}

		/// <summary>
		/// Restricciones de topología de sesión (<c>single</c>/<c>tracks</c>/<c>limit</c>/<c>vmax</c>).
		/// </summary>
		public IReadOnlyList<DemandTopoConstraint> TopoConstraints
		{
			get { return mcolTopoConstraints; }
		}

		/// <summary>
		/// Tipos de tren declarados con <c>train</c>/<c>tren</c> (catálogo de flota del script).
		/// Vacío si el script no define ninguno (el plan usará el modelo por defecto).
		/// </summary>
		public IReadOnlyList<TrainSpecs> Fleet
		{
			get { return mcolFleet; }
		}

		public IReadOnlyList<string> Errors
		{
			get { return mcolErrors; }
		}

		public bool Success
		{
			get { return mcolErrors.Count == 0; }
		}

		internal void AddRequirement(DemandRequirement requirement)
		{
			mcolRequirements.Add(requirement);
		}

		internal void AddDelete(DemandDeleteOp deleteOp)
		{
			mcolDeletes.Add(deleteOp);
		}

		internal void AddAsimilationDef(DemandAsimilationDef definition)
		{
			mcolAsimilationDefs.Add(definition);
		}

		internal void AddTopoConstraint(DemandTopoConstraint constraint)
		{
			mcolTopoConstraints.Add(constraint);
		}

		internal void AddFleet(TrainSpecs specs)
		{
			if (specs is not null)
			{
				mcolFleet.Add(specs);
			}
		}

		internal void AddError(string message)
		{
			mcolErrors.Add(message);
		}

		internal void AddError(int line, string message)
		{
			mcolErrors.Add("line " + line.ToString(System.Globalization.CultureInfo.InvariantCulture) + ": " + message);
		}
	}
}
