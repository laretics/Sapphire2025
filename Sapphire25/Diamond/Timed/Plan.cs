using System;
using System.Collections.Generic;
using Diamond.Motion;
using Diamond.Topo;

namespace Diamond.Timed
{
	/// <summary>
	/// Proyecto de horarios de trenes (malla).
	/// Fuente de verdad de la demanda: script mini-DSL (compilación determinista).
	/// La topología puede declararse en el script con <c>include nombre-topo</c>
	/// (extensión <c>.xml</c> implícita) y se carga vía <see cref="TopoStorage"/> al compilar.
	/// </summary>
	public sealed class Plan
	{
		private string mvarId;
		private string mvarName;
		private string mvarComment;
		private string mvarNotes;
		private TopoLayout? mvarTopo;
		private TopoStorage? mvarTopoStorage;
		private string mvarScriptBaseDirectory;
		private readonly List<TrainSpecs> mcolTrainSpecs;
		private readonly List<DemandRequirement> mcolDemand;
		private readonly List<DemandDeleteOp> mcolDeletes;
		private readonly List<DemandAsimilationDef> mcolAsimilationDefs;
		private readonly List<DemandTopoConstraint> mcolTopoConstraints;
		private string mvarDemandScript;

		public Plan()
		{
			mvarId = string.Empty;
			mvarName = string.Empty;
			mvarComment = string.Empty;
			mvarNotes = string.Empty;
			mvarTopo = null;
			mvarTopoStorage = null;
			mvarScriptBaseDirectory = string.Empty;
			mcolTrainSpecs = new List<TrainSpecs>();
			mcolDemand = new List<DemandRequirement>();
			mcolDeletes = new List<DemandDeleteOp>();
			mcolAsimilationDefs = new List<DemandAsimilationDef>();
			mcolTopoConstraints = new List<DemandTopoConstraint>();
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

		public Plan(TopoStorage topoStorage)
			: this()
		{
			if (topoStorage is null)
			{
				throw new ArgumentNullException(nameof(topoStorage));
			}

			mvarTopoStorage = topoStorage;
			mvarTopo = topoStorage.Layout;
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
		/// Notas del plan (directiva <c>notes</c> del mini-DSL). Usadas p. ej. en el cajetín de impresión.
		/// </summary>
		public string Notes
		{
			get { return mvarNotes; }
			set { mvarNotes = value ?? string.Empty; }
		}

		/// <summary>
		/// Topología asociada. Puede fijarse a mano o cargarse desde
		/// <c>include</c> al compilar el script (<see cref="TopoStorage"/>).
		/// </summary>
		public TopoLayout? Topo
		{
			get { return mvarTopo; }
			set
			{
				mvarTopo = value;
				// Asignación explícita: ya no hay vínculo con un include previo.
				if (mvarTopoStorage is not null && !ReferenceEquals(mvarTopoStorage.Layout, value))
				{
					mvarTopoStorage = null;
				}
			}
		}

		/// <summary>
		/// Almacén de topología cargado por <c>include</c> (ruta + layout).
		/// Null si la topología se inyectó sin pasar por el script.
		/// </summary>
		public TopoStorage? TopoStorage
		{
			get { return mvarTopoStorage; }
		}

		/// <summary>
		/// Directorio base para resolver rutas relativas de <c>include</c>
		/// (p. ej. carpeta del script o de samples). Vacío = cwd.
		/// </summary>
		public string ScriptBaseDirectory
		{
			get { return mvarScriptBaseDirectory; }
			set { mvarScriptBaseDirectory = value ?? string.Empty; }
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
		/// Directivas <c>delete</c> compiladas (orden del script; ver <see cref="DemandDeleteOp.ScriptOrder"/>).
		/// </summary>
		public IReadOnlyList<DemandDeleteOp> Deletes
		{
			get { return mcolDeletes; }
		}

		/// <summary>
		/// Definiciones <c>asim</c> (numeración xx## y color por corredor OD / día).
		/// </summary>
		public IReadOnlyList<DemandAsimilationDef> AsimilationDefs
		{
			get { return mcolAsimilationDefs; }
		}

		/// <summary>
		/// Restricciones de topología de sesión del script (<c>single</c>, <c>tracks</c>, <c>limit</c>, <c>vmax</c>).
		/// Se aplican a <see cref="Axis.SessionLimits"/> / <see cref="Axis.SessionTrackSpans"/> al compilar.
		/// </summary>
		public IReadOnlyList<DemandTopoConstraint> TopoConstraints
		{
			get { return mcolTopoConstraints; }
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
		/// Si el script tiene <c>include</c>, carga el XML en <see cref="TopoStorage"/> / <see cref="Topo"/>.
		/// Si hay topología, resuelve estaciones. Devuelve el resultado (errores incluidos).
		/// </summary>
		public DemandCompileResult CompileDemand()
		{
			return CompileDemand(mvarDemandScript, resolveStations: true);
		}

		/// <summary>
		/// Compila el script indicado, lo guarda en <see cref="DemandScript"/> y actualiza <see cref="Demand"/>.
		/// Aplica <c>include</c> de topología si está presente.
		/// </summary>
		public DemandCompileResult CompileDemand(string script, bool resolveStations = true)
		{
			mvarDemandScript = script ?? string.Empty;
			DemandCompileResult result = DemandScriptParser.Parse(mvarDemandScript);

			// Cabecera del script: plan y notes son fuente de verdad al compilar.
			if (result.PlanName.Length > 0)
			{
				mvarName = result.PlanName;
			}

			mvarNotes = result.Notes;

			mcolDemand.Clear();
			mcolDeletes.Clear();
			mcolAsimilationDefs.Clear();
			mcolTopoConstraints.Clear();

			// include topo: carga el XML y fija Topo / TopoStorage (fuente de verdad del script).
			if (result.IncludedTopoPath.Length > 0)
			{
				ApplyIncludedTopo(result);
			}

			if (!result.Success)
			{
				// Script inválido: quitar overlays de sesión residuales.
				if (mvarTopo is not null)
				{
					DemandTopoOverlay.Clear(mvarTopo);
				}

				return result;
			}

			int index = 0;
			while (index < result.Requirements.Count)
			{
				mcolDemand.Add(result.Requirements[index]);
				index++;
			}

			index = 0;
			while (index < result.Deletes.Count)
			{
				mcolDeletes.Add(result.Deletes[index]);
				index++;
			}

			index = 0;
			while (index < result.AsimilationDefs.Count)
			{
				mcolAsimilationDefs.Add(result.AsimilationDefs[index]);
				index++;
			}

			index = 0;
			while (index < result.TopoConstraints.Count)
			{
				mcolTopoConstraints.Add(result.TopoConstraints[index]);
				index++;
			}

			ApplyCompiledFleet(result);

			// Si hubo include, la resolución de estaciones es necesaria para un plan usable.
			bool shouldResolve = resolveStations || result.IncludedTopoPath.Length > 0;

			if (shouldResolve && mvarTopo is not null)
			{
				List<string> resolveErrors = new List<string>();
				DemandStationResolver.Resolve(result, mvarTopo, resolveErrors);
				// Capas de sesión: vías simples y límites del script (no tocan la topo base).
				DemandTopoOverlay.Apply(mvarTopo, mcolTopoConstraints, resolveErrors);
				int e = 0;
				while (e < resolveErrors.Count)
				{
					result.AddError(resolveErrors[e]);
					e++;
				}
			}
			else if (mvarTopo is not null)
			{
				// Sin resolución de estaciones no se pueden aplicar tramos; limpiar sesión.
				DemandTopoOverlay.Clear(mvarTopo);
			}

			return result;
		}

		/// <summary>
		/// Aplica el catálogo <c>train</c> del script. Si el script no define ninguno,
		/// asegura el modelo por defecto (<see cref="TrainSpecs.DefaultModel"/>).
		/// </summary>
		private void ApplyCompiledFleet(DemandCompileResult result)
		{
			if (result.Fleet.Count > 0)
			{
				mcolTrainSpecs.Clear();
				int index = 0;
				while (index < result.Fleet.Count)
				{
					mcolTrainSpecs.Add(result.Fleet[index]);
					index++;
				}

				return;
			}

			// Sin declaraciones en el script: conservar catálogo del host y garantizar default.
			EnsureDefaultTrainSpecs();
		}

		/// <summary>
		/// Carga el XML declarado por <c>include</c> y actualiza <see cref="Topo"/> / <see cref="TopoStorage"/>.
		/// </summary>
		private void ApplyIncludedTopo(DemandCompileResult result)
		{
			string? baseDir = mvarScriptBaseDirectory.Length > 0 ? mvarScriptBaseDirectory : null;
			TopoStorage? storage;
			string? error;
			if (!TopoStorage.TryLoadFromXml(result.IncludedTopoPath, baseDir, out storage, out error) || storage is null)
			{
				result.AddError(error ?? "no se pudo cargar la topología del include.");
				return;
			}

			// Sustituir topología previa de sesión (overlays) si había otra.
			if (mvarTopo is not null && !ReferenceEquals(mvarTopo, storage.Layout))
			{
				DemandTopoOverlay.Clear(mvarTopo);
			}

			mvarTopoStorage = storage;
			mvarTopo = storage.Layout;
		}

		/// <summary>
		/// Reaplica las restricciones de topología de sesión ya compiladas (p. ej. al planificar).
		/// </summary>
		public void ApplyTopoSessionOverlays()
		{
			if (mvarTopo is null)
			{
				return;
			}

			DemandTopoOverlay.Apply(mvarTopo, mcolTopoConstraints, errors: null);
		}

		public void ClearDemand()
		{
			mcolDemand.Clear();
			mcolDeletes.Clear();
			mcolAsimilationDefs.Clear();
			mcolTopoConstraints.Clear();
			mvarDemandScript = string.Empty;
			if (mvarTopo is not null)
			{
				DemandTopoOverlay.Clear(mvarTopo);
			}
		}

		/// <summary>
		/// Quita la topología asociada (include o asignación manual) y limpia overlays de sesión.
		/// </summary>
		public void ClearTopo()
		{
			if (mvarTopo is not null)
			{
				DemandTopoOverlay.Clear(mvarTopo);
			}

			mvarTopo = null;
			mvarTopoStorage = null;
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
