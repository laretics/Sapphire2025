namespace Diamond.Controls.Rendering
{
	/// <summary>
	/// Paleta de la hoja de circulación: impresión oficial o cabina día / noche.
	/// </summary>
	public readonly struct CirculationSheetPalette
	{
		private CirculationSheetPalette(
			string background,
			string text,
			string stroke,
			string headerFill,
			string headerText,
			string vmxFill,
			string temporaryVmxFill,
			string temporaryDepText,
			string subHeaderFill,
			string mutedStroke,
			string indexGroupFill,
			string qrModule,
			string qrPaper)
		{
			Background = background;
			Text = text;
			Stroke = stroke;
			HeaderFill = headerFill;
			HeaderText = headerText;
			VmxFill = vmxFill;
			TemporaryVmxFill = temporaryVmxFill;
			TemporaryDepText = temporaryDepText;
			SubHeaderFill = subHeaderFill;
			MutedStroke = mutedStroke;
			IndexGroupFill = indexGroupFill;
			QrModule = qrModule;
			QrPaper = qrPaper;
		}

		/// <summary>Fondo de la hoja.</summary>
		public string Background { get; }

		/// <summary>Textos de tabla (dependencias, horas, PK…).</summary>
		public string Text { get; }

		/// <summary>Trazos (rejilla, líderes, marco).</summary>
		public string Stroke { get; }

		/// <summary>Relleno de bandas de cabecera.</summary>
		public string HeaderFill { get; }

		/// <summary>Texto sobre bandas de cabecera.</summary>
		public string HeaderText { get; }

		/// <summary>Fondo de la columna Max.</summary>
		public string VmxFill { get; }

		/// <summary>Fondo de Max cuando el tramo es limitación temporal.</summary>
		public string TemporaryVmxFill { get; }

		/// <summary>Texto de motivo/observaciones de temporal en Dependencia.</summary>
		public string TemporaryDepText { get; }

		/// <summary>Franja Loc./ruta bajo el título.</summary>
		public string SubHeaderFill { get; }

		/// <summary>Separador de mitades / líneas suaves.</summary>
		public string MutedStroke { get; }

		/// <summary>Fondo de grupo en el índice del libro.</summary>
		public string IndexGroupFill { get; }

		/// <summary>Módulos oscuros del QR.</summary>
		public string QrModule { get; }

		/// <summary>Fondo del QR.</summary>
		public string QrPaper { get; }

		/// <summary>Impresión Zafiro: negro sobre blanco (no cambiar).</summary>
		public static CirculationSheetPalette Print
		{
			get
			{
				return new CirculationSheetPalette(
					background: "#ffffff",
					text: "#000",
					stroke: "#000",
					headerFill: "#747474",
					headerText: "#fff",
					vmxFill: "#e8e8e8",
					temporaryVmxFill: "#ffd400",
					temporaryDepText: "#c00000",
					subHeaderFill: "#f5f5f5",
					mutedStroke: "#ccc",
					indexGroupFill: "#eee",
					qrModule: "#000",
					qrPaper: "#fff");
			}
		}

		/// <summary>Cabina, modo día: trazos azul oscuro, textos negros, fondo blanco.</summary>
		public static CirculationSheetPalette CabinDay
		{
			get
			{
				return new CirculationSheetPalette(
					background: "#ffffff",
					text: "#000000",
					stroke: "#123a6b",
					headerFill: "#123a6b",
					headerText: "#ffffff",
					vmxFill: "#d6e4f0",
					temporaryVmxFill: "#ffd400",
					temporaryDepText: "#c00000",
					subHeaderFill: "#eef3f8",
					mutedStroke: "#3a5a8a",
					indexGroupFill: "#dce6f2",
					qrModule: "#123a6b",
					qrPaper: "#ffffff");
			}
		}

		/// <summary>Cabina, modo noche: trazos marrón claro, letras rojo suave, fondo negro.</summary>
		public static CirculationSheetPalette CabinNight
		{
			get
			{
				return new CirculationSheetPalette(
					background: "#000000",
					text: "#e07070",
					stroke: "#c9a27a",
					headerFill: "#2a1c12",
					headerText: "#e07070",
					vmxFill: "#1a120c",
					temporaryVmxFill: "#5a3d00",
					temporaryDepText: "#ff6b6b",
					subHeaderFill: "#120c08",
					mutedStroke: "#8a7058",
					indexGroupFill: "#1a120c",
					qrModule: "#c9a27a",
					qrPaper: "#000000");
			}
		}

		public static CirculationSheetPalette ForCabin(bool nightMode)
		{
			return nightMode ? CabinNight : CabinDay;
		}
	}
}
