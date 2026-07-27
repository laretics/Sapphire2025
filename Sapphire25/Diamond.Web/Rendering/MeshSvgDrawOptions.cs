namespace Diamond.Web.Rendering
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
		private readonly TimeSpan? mvarNowTime;
		private readonly int mvarMaxPolylineSamples;

		public MeshSvgDrawOptions(
			bool showCantonOccupations,
			bool showTrainPaths,
			bool showTrainNumbers,
			bool showConflicts,
			bool showSpeedStrip,
			bool showTrackStrip,
			bool showNowLine,
			TimeSpan? nowTime,
			int maxPolylineSamples)
		{
			mvarShowCantonOccupations = showCantonOccupations;
			mvarShowTrainPaths = showTrainPaths;
			mvarShowTrainNumbers = showTrainNumbers;
			mvarShowConflicts = showConflicts;
			mvarShowSpeedStrip = showSpeedStrip;
			mvarShowTrackStrip = showTrackStrip;
			mvarShowNowLine = showNowLine;
			mvarNowTime = nowTime;
			mvarMaxPolylineSamples = maxPolylineSamples < 8 ? 8 : maxPolylineSamples;
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
				maxPolylineSamples: maxPolylineSamples)
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
			int maxPolylineSamples = 48)
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
				maxPolylineSamples);
		}

		/// <summary>
		/// Variante ligera para pan/zoom: respeta capas del usuario salvo ocupaciones/números
		/// (caras) y reduce el muestreo de polilíneas.
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
				maxPolylineSamples: 16);
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

		/// <summary>Tope de segmentos por polilínea (además del presupuesto por píxeles).</summary>
		public int MaxPolylineSamples
		{
			get { return mvarMaxPolylineSamples; }
		}
	}
}
