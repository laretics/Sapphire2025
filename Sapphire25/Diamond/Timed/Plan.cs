using System;
using System.Collections.Generic;
using Diamond.Motion;
using Diamond.Topo;

namespace Diamond.Timed
{
	/// <summary>
	/// Proyecto de horarios de trenes (malla).
	/// Fuente de verdad de la demanda: script mini-DSL (compilación determinista).
	/// </summary>
	public sealed class Plan
	{
		private string mvarId;
		private string mvarName;
		private string mvarComment;
		private TopoLayout? mvarTopo;
		private readonly List<TrainSpecs> mcolTrainSpecs;
		private readonly List<DemandRequirement> mcolDemand;
		private string mvarDemandScript;

		public Plan()
		{
			mvarId = string.Empty;
			mvarName = string.Empty;
			mvarComment = string.Empty;
			mvarTopo = null;
			mcolTrainSpecs = new List<TrainSpecs>();
			mcolDemand = new List<DemandRequirement>();
			mvarDemandScript = string.Empty;
		}

		public Plan(TopoLayout topo)
			: this()
		{
			if (topo is null)
			{
				throw new ArgumentNullException(nameof(topo));
			}

			mvarTopo = topo;
		}

		/// <summary>
		/// Identificador del plan / proyecto de malla.
		/// </summary>
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

		public string Comment
		{
			get { return mvarComment; }
			set { mvarComment = value ?? string.Empty; }
		}

		/// <summary>
		/// Topología asociada (opcional en esta fase).
		/// </summary>
		public TopoLayout? Topo
		{
			get { return mvarTopo; }
			set { mvarTopo = value; }
		}

		/// <summary>
		/// Catálogo de tipos de tren (<see cref="Motion.TrainSpecs"/>) disponibles en el plan.
		/// </summary>
		public IReadOnlyList<TrainSpecs> Fleet
		{
			get { return mcolTrainSpecs; }
		}

		/// <summary>
		/// Requisitos de demanda compilados (orden del script).
		/// </summary>
		public IReadOnlyList<DemandRequirement> Demand
		{
			get { return mcolDemand; }
		}

		/// <summary>
		/// Script fuente de la demanda (mini-DSL). Compilar con <see cref="CompileDemand"/>.
		/// </summary>
		public string DemandScript
		{
			get { return mvarDemandScript; }
			set { mvarDemandScript = value ?? string.Empty; }
		}

		public void AddTrainSpecs(TrainSpecs specs)
		{
			if (specs is null)
			{
				throw new ArgumentNullException(nameof(specs));
			}

			if (specs.Id.Length > 0 && FindTrainSpecsById(specs.Id) is not null)
			{
				throw new InvalidOperationException($"Ya existe un TrainSpecs con id '{specs.Id}'.");
			}

			mcolTrainSpecs.Add(specs);
		}

		public bool RemoveTrainSpecs(TrainSpecs specs)
		{
			if (specs is null)
			{
				return false;
			}

			return mcolTrainSpecs.Remove(specs);
		}

		public TrainSpecs? FindTrainSpecsById(string id)
		{
			if (id is null)
			{
				return null;
			}

			int index = 0;
			while (index < mcolTrainSpecs.Count)
			{
				if (string.Equals(mcolTrainSpecs[index].Id, id, StringComparison.Ordinal))
				{
					return mcolTrainSpecs[index];
				}

				index++;
			}

			return null;
		}

		public void ClearTrainSpecs()
		{
			mcolTrainSpecs.Clear();
		}

		/// <summary>
		/// Asegura que el catálogo incluye el tren modelo por defecto (id = "default").
		/// </summary>
		public TrainSpecs EnsureDefaultTrainSpecs()
		{
			TrainSpecs? existing = FindTrainSpecsById("default");
			if (existing is not null)
			{
				return existing;
			}

			TrainSpecs created = Motion.TrainSpecs.DefaultModel;
			mcolTrainSpecs.Add(created);
			return created;
		}

		/// <summary>
		/// Compila <see cref="DemandScript"/> de forma determinista y sustituye <see cref="Demand"/>.
		/// Si hay <see cref="Topo"/>, resuelve estaciones. Devuelve el resultado (errores incluidos).
		/// </summary>
		public DemandCompileResult CompileDemand()
		{
			return CompileDemand(mvarDemandScript, resolveStations: mvarTopo is not null);
		}

		/// <summary>
		/// Compila el script indicado, lo guarda en <see cref="DemandScript"/> y actualiza <see cref="Demand"/>.
		/// </summary>
		public DemandCompileResult CompileDemand(string script, bool resolveStations = true)
		{
			mvarDemandScript = script ?? string.Empty;
			DemandCompileResult result = DemandScriptParser.Parse(mvarDemandScript);

			if (result.PlanName.Length > 0 && mvarName.Length == 0)
			{
				mvarName = result.PlanName;
			}

			mcolDemand.Clear();
			if (!result.Success)
			{
				return result;
			}

			int index = 0;
			while (index < result.Requirements.Count)
			{
				mcolDemand.Add(result.Requirements[index]);
				index++;
			}

			if (resolveStations && mvarTopo is not null)
			{
				List<string> resolveErrors = new List<string>();
				DemandStationResolver.Resolve(result, mvarTopo, resolveErrors);
				int e = 0;
				while (e < resolveErrors.Count)
				{
					result.AddError(resolveErrors[e]);
					e++;
				}
			}

			return result;
		}

		public void ClearDemand()
		{
			mcolDemand.Clear();
			mvarDemandScript = string.Empty;
		}

		public override string ToString()
		{
			if (mvarName.Length > 0)
			{
				return mvarName;
			}

			if (mvarId.Length > 0)
			{
				return mvarId;
			}

			return "Plan";
		}
	}
}
