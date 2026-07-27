namespace Diamond.Controls.Rendering
{
	/// <summary>
	/// Geometría del diagrama de malla (chrome vs zona de plot).
	/// Soporta columna de estaciones integrada en el SVG o externa.
	/// </summary>
	public static class MeshSvgLayout
	{
		public const double StationColumnWidth = 120;
		public const double StripWidth = 11;
		public const double MarginTop = 36;
		public const double MarginBottom = 48;
		public const double MarginRight = 24;

		/// <summary>Compat: etiqueta histórica.</summary>
		public const double StationLabelArea = StationColumnWidth;

		public static double GetPlotLeft(bool externalStationColumn)
		{
			if (externalStationColumn)
			{
				return StripWidth + StripWidth;
			}

			return StationColumnWidth + StripWidth + StripWidth;
		}

		public static double PlotLeft
		{
			get { return GetPlotLeft(false); }
		}

		public static double PlotTop
		{
			get { return MarginTop; }
		}

		public static double GetSpeedStripX(bool externalStationColumn)
		{
			if (externalStationColumn)
			{
				return 0;
			}

			return StationColumnWidth;
		}

		public static double SpeedStripX
		{
			get { return GetSpeedStripX(false); }
		}

		public static double GetTrackStripX(bool externalStationColumn)
		{
			return GetSpeedStripX(externalStationColumn) + StripWidth;
		}

		public static double TrackStripX
		{
			get { return GetTrackStripX(false); }
		}

		public static double GetPlotWidth(int svgWidth, bool externalStationColumn)
		{
			double w = svgWidth - GetPlotLeft(externalStationColumn) - MarginRight;
			return w < 100 ? 100 : w;
		}

		public static double PlotWidth(int svgWidth)
		{
			return GetPlotWidth(svgWidth, false);
		}

		public static double PlotHeight(int svgHeight)
		{
			double h = svgHeight - MarginTop - MarginBottom;
			return h < 100 ? 100 : h;
		}

		public static bool IsInsidePlot(double x, double y, int svgWidth, int svgHeight, bool externalStationColumn)
		{
			double left = GetPlotLeft(externalStationColumn);
			double top = PlotTop;
			double w = GetPlotWidth(svgWidth, externalStationColumn);
			double h = PlotHeight(svgHeight);
			return x >= left && x <= left + w && y >= top && y <= top + h;
		}

		public static bool IsInsidePlot(double x, double y, int svgWidth, int svgHeight)
		{
			return IsInsidePlot(x, y, svgWidth, svgHeight, false);
		}

		public static double TimeAtX(double x, TimeSpan t0, TimeSpan t1, int svgWidth, bool externalStationColumn)
		{
			double u = (x - GetPlotLeft(externalStationColumn)) / GetPlotWidth(svgWidth, externalStationColumn);
			if (u < 0)
			{
				u = 0;
			}

			if (u > 1)
			{
				u = 1;
			}

			return t0.TotalSeconds + u * (t1.TotalSeconds - t0.TotalSeconds);
		}

		public static double TimeAtX(double x, TimeSpan t0, TimeSpan t1, int svgWidth)
		{
			return TimeAtX(x, t0, t1, svgWidth, false);
		}

		public static double PkAtY(double y, long pkMin, long pkMax, int svgHeight)
		{
			double u = 1.0 - (y - PlotTop) / PlotHeight(svgHeight);
			if (u < 0)
			{
				u = 0;
			}

			if (u > 1)
			{
				u = 1;
			}

			return pkMin + u * (pkMax - pkMin);
		}

		public static double PkToY(long pk, long pkMin, long pkMax, double plotTop, double plotH)
		{
			return plotTop + (1.0 - (double)(pk - pkMin) / (pkMax - pkMin)) * plotH;
		}
	}
}
