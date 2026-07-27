using System.Collections.Generic;

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
		private readonly List<string> mcolErrors;
		private string mvarPlanName;

		public DemandCompileResult()
		{
			mcolRequirements = new List<DemandRequirement>();
			mcolDeletes = new List<DemandDeleteOp>();
			mcolAsimilationDefs = new List<DemandAsimilationDef>();
			mcolErrors = new List<string>();
			mvarPlanName = string.Empty;
		}

		public string PlanName
		{
			get { return mvarPlanName; }
			internal set { mvarPlanName = value ?? string.Empty; }
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
