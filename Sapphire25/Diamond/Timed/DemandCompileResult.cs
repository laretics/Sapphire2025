using System.Collections.Generic;

namespace Diamond.Timed
{
	/// <summary>
	/// Resultado determinista de compilar un script de demanda.
	/// </summary>
	public sealed class DemandCompileResult
	{
		private readonly List<DemandRequirement> mcolRequirements;
		private readonly List<string> mcolErrors;
		private string mvarPlanName;

		public DemandCompileResult()
		{
			mcolRequirements = new List<DemandRequirement>();
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
