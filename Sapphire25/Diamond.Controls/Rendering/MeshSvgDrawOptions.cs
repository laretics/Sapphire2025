namespace Diamond.Controls.Rendering
{
	/// <summary>
	/// Capas y nivel de detalle del SVG de malla.
	/// En interacción (pan/zoom) se reduce el muestreo y se omiten capas pesadas.
	/// </summary>
	public readonly struct MeshSvgDrawOptions
	{
		private readonly bool mvarShowCantonOccupations;
		private readonly bool mvarShowTrainPaths;
		private readonly bool mvarShowTrainNumbers;
		private readonly bool mvarShowConflicts;
		private readonly bool mvarShowSpeedStrip;
		private readonly bool mvarShowTrackStrip;
		private readonly bool mvarShowNowLine;
		private readonly bool mvarShowStationLabels;
		private readonly bool mvarExternalStationColumn;
		private readonly MeshYScaleMode mvarYScaleMode;
		private readonly TimeSpan? mvarNowTime;
		private readonly int mvarMaxPolylineSamples;
		private readonly string? mvarSelectedTechnicalId;

		public MeshSvgDrawOptions(
			bool showCantonOccupations,
			bool showTrainPaths,
			bool showTrainNumbers,
			bool showConflicts,
			bool showSpeedStrip,
			bool showTrackStrip,
			bool showNowLine,
			TimeSpan? nowTime,
			int maxPolylineSamples,
			bool showStationLabels = true,
			bool externalStationColumn = false,
			MeshYScaleMode yScaleMode = MeshYScaleMode.LinearPk,
			string? selectedTechnicalId = null)
		{
			mvarShowCantonOccupations = showCantonOccupations;
			mvarShowTrainPaths = showTrainPaths;
			mvarShowTrainNumbers = showTrainNumbers;
			mvarShowConflicts = showConflicts;
			mvarShowSpeedStrip = showSpeedStrip;
			mvarShowTrackStrip = showTrackStrip;
			mvarShowNowLine = showNowLine;
			mvarShowStationLabels = showStationLabels;
			mvarExternalStationColumn = externalStationColumn;
			mvarYScaleMode = yScaleMode;
			mvarNowTime = nowTime;
			mvarMaxPolylineSamples = maxPolylineSamples < 8 ? 8 : maxPolylineSamples;
			mvarSelectedTechnicalId = string.IsNullOrEmpty(selectedTechnicalId) ? null : selectedTechnicalId;
		}

		/// <summary>Compatibilidad: capas completas por defecto.</summary>
		public MeshSvgDrawOptions(
			bool showCantonOccupations,
			bool showTrainNumbers,
			bool showConflicts,
			int maxPolylineSamples)
			: this(
				showCantonOccupations: showCantonOccupations,
				showTrainPaths: true,
				showTrainNumbers: showTrainNumbers,
				showConflicts: showConflicts,
				showSpeedStrip: true,
				showTrackStrip: true,
				showNowLine: false,
				nowTime: null,
				maxPolylineSamples: maxPolylineSamples,
				showStationLabels: true,
				externalStationColumn: false)
		{
		}

		/// <summary>Calidad completa al soltar el pan/zoom o al planificar.</summary>
		public static MeshSvgDrawOptions Full
		{
			get
			{
				return Create();
			}
		}

		/// <summary>Durante arrastre/rueda: trazas baratas, sin números ni ocupaciones.</summary>
		public static MeshSvgDrawOptions Interactive
		{
			get
			{
				return Create(
					showCantonOccupations: false,
					showTrainNumbers: false,
					maxPolylineSamples: 16);
			}
		}

		public static MeshSvgDrawOptions Create(
			bool showCantonOccupations = true,
			bool showTrainPaths = true,
			bool showTrainNumbers = true,
			bool showConflicts = true,
			bool showSpeedStrip = true,
			bool showTrackStrip = true,
			bool showNowLine = false,
			TimeSpan? nowTime = null,
			int maxPolylineSamples = 48,
			bool showStationLabels = true,
			bool externalStationColumn = false,
			MeshYScaleMode yScaleMode = MeshYScaleMode.LinearPk,
			string? selectedTechnicalId = null)
		{
			return new MeshSvgDrawOptions(
				showCantonOccupations,
				showTrainPaths,
				showTrainNumbers,
				showConflicts,
				showSpeedStrip,
				showTrackStrip,
				showNowLine,
				nowTime,
				maxPolylineSamples,
				showStationLabels,
				externalStationColumn,
				yScaleMode,
				selectedTechnicalId);
		}

		/// <summary>
		/// Variante ligera para pan/zoom: omite ocupaciones/numeros y reduce muestreo.
		/// </summary>
		public MeshSvgDrawOptions ForInteractiveLod()
		{
			return new MeshSvgDrawOptions(
				showCantonOccupations: false,
				showTrainPaths: mvarShowTrainPaths,
				showTrainNumbers: false,
				showConflicts: mvarShowConflicts,
				showSpeedStrip: mvarShowSpeedStrip,
				showTrackStrip: mvarShowTrackStrip,
				showNowLine: mvarShowNowLine,
				nowTime: mvarNowTime,
				maxPolylineSamples: 16,
				showStationLabels: mvarShowStationLabels,
				externalStationColumn: mvarExternalStationColumn,
				yScaleMode: mvarYScaleMode,
				selectedTechnicalId: mvarSelectedTechnicalId);
		}

		public bool ShowCantonOccupations
		{
			get { return mvarShowCantonOccupations; }
		}

		public bool ShowTrainPaths
		{
			get { return mvarShowTrainPaths; }
		}

		public bool ShowTrainNumbers
		{
			get { return mvarShowTrainNumbers; }
		}

		public bool ShowConflicts
		{
			get { return mvarShowConflicts; }
		}

		public bool ShowSpeedStrip
		{
			get { return mvarShowSpeedStrip; }
		}

		public bool ShowTrackStrip
		{
			get { return mvarShowTrackStrip; }
		}

		public bool ShowNowLine
		{
			get { return mvarShowNowLine; }
		}

		public TimeSpan? NowTime
		{
			get { return mvarNowTime; }
		}

		/// <summary>Etiquetas de estacion dentro del SVG (false si usa control StationRuler).</summary>
		public bool ShowStationLabels
		{
			get { return mvarShowStationLabels; }
		}

		/// <summary>Geometria SVG sin hueco izquierdo de estaciones (columna externa).</summary>
		public bool ExternalStationColumn
		{
			get { return mvarExternalStationColumn; }
		}

		/// <summary>Escala del eje espacial (PK lineal o escalonada por singulares).</summary>
		public MeshYScaleMode YScaleMode
		{
			get { return mvarYScaleMode; }
		}

		/// <summary>
		/// <see cref="Diamond.Timed.Circulation.TechnicalId"/> de la circulación seleccionada
		/// (resaltado de traza). Null = ninguna.
		/// </summary>
		public string? SelectedTechnicalId
		{
			get { return mvarSelectedTechnicalId; }
		}

		/// <summary>Tope de segmentos por polilinea.</summary>
		public int MaxPolylineSamples
		{
			get { return mvarMaxPolylineSamples; }
		}
	}
}
