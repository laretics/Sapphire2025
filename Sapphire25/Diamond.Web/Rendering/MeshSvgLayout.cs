namespace Diamond.Web.Rendering
{
	/// <summary>
	/// Geometría fija del diagrama de malla (chrome vs zona de plot).
	/// Las reglas de estaciones/tiempo viven fuera del plot; el plot se pan/zoom en dominio datos.
	/// </summary>
	public static class MeshSvgLayout
	{
		public const double StationLabelArea = 120;
		public const double StripWidth = 11;
		public const double MarginTop = 36;
		public const double MarginBottom = 48;
		public const double MarginRight = 24;

		public static double PlotLeft
		{
			get { return StationLabelArea + StripWidth + StripWidth; }
		}

		public static double PlotTop
		{
			get { return MarginTop; }
		}

		public static double SpeedStripX
		{
			get { return StationLabelArea; }
		}

		public static double TrackStripX
		{
			get { return StationLabelArea + StripWidth; }
		}

		public static double PlotWidth(int svgWidth)
		{
			double w = svgWidth - PlotLeft - MarginRight;
			return w < 100 ? 100 : w;
		}

		public static double PlotHeight(int svgHeight)
		{
			double h = svgHeight - MarginTop - MarginBottom;
			return h < 100 ? 100 : h;
		}

		public static bool IsInsidePlot(double x, double y, int svgWidth, int svgHeight)
		{
			double left = PlotLeft;
			double top = PlotTop;
			double w = PlotWidth(svgWidth);
			double h = PlotHeight(svgHeight);
			return x >= left && x <= left + w && y >= top && y <= top + h;
		}

		public static double TimeAtX(double x, TimeSpan t0, TimeSpan t1, int svgWidth)
		{
			double u = (x - PlotLeft) / PlotWidth(svgWidth);
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
	}
}
