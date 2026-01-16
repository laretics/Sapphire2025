using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimeNet2026.Timed;
using TimeNet2026.Topo;

namespace TimeNetComponents.Controls.TimeNetControl
{
	public class TimenetEnvironmentY
	{
		public int Height { get; set; }
		public bool PkMode { get; set; } = true;
		internal int LowerMargin => Height - 40;
		internal int UpperMargin = 20;
		internal int MiddleSize => LowerMargin - UpperMargin;
		internal int DownSize => Height - LowerMargin;

		public long MaxValue => mvarView.MaxPk;
		public long MinValue => 0;

		public long Zoom { get; set; } = 3000; //Número de metros visible en esta vista.
		public long Offset { get; set; } = 0; //Desplazamiento en metros.

		public long MaxZoom => mvarView.MaxPk;

		// Nueva propiedad: Desplazamiento de visualización en píxeles (similar a DisplayOffsetXPx)
		internal double DisplayOffsetYPx => -(MiddleSize * Offset) / (double)Zoom;

		internal long mvarMinPk;
		internal long mvarMaxPk;
		internal AsimilationView mvarView;

		public TimenetEnvironmentY(int height, TopoStorage? storage, Asimilation? asimilation)
		{
			this.Height = height;
			TopoStorage auxStorage;
			Asimilation auxAsimila;
			auxStorage = null == storage ? new TopoStorage() : storage;
			auxAsimila = null == asimilation ? new Asimilation(auxStorage) : asimilation;
			mvarView = new AsimilationView(auxAsimila, auxStorage);
			Zoom = MaxZoom;
		}

		// GetY ahora calcula posiciones relativas al zoom (no absolutas)
		internal double GetY(Station? station)
		{
			StationViewRef? punto = GetReference(station);
			{
				if (null != punto)
				{
					System.Diagnostics.Debug.Assert(null != mvarView);
					double auxValor = -1;
					if (PkMode && Zoom > 0)
						auxValor = MiddleSize * punto.ViewPk / (double)Zoom;
					else if (mvarView.MaxIndex > 0)
						auxValor = MiddleSize * punto.Index / mvarView.MaxIndex;

					if (auxValor >= 0)
						return auxValor;
				}
			}
			return -1;
		}

		internal StationViewRef? GetReference(Station? station)
		{
			if (null != mvarView && null != station && mvarView.Elements.ContainsKey(station))
				return mvarView.Elements[station];
			return null;
		}

		internal void ZoomIn()
		{
			Zoom = Math.Max(100, Zoom / 2);
			updateSliderParams();
		}
		internal void ZoomOut()
		{
			Zoom = Math.Min(MaxZoom, Zoom * 2);
			updateSliderParams();
		}

		internal void updateSliderParams()
		{
			long maxOffset = Math.Max(0, MaxValue - Zoom);
			if (Offset > maxOffset)
				Offset = maxOffset;
			if (Offset < 0)
				Offset = 0;
		}
	}
}
