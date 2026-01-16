using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimeNet2026.Timed;

namespace TimeNetComponents.Controls.TimeNetControl
{
    public class TimeNetEnvironmentX
    {
        private const int TOTAL_HOURS = 24;
        internal const int MAX_X_OFFSET = 3600 * TOTAL_HOURS;
		internal const int MIN_POSITION = 0;
        internal const int MAX_POSITION = MAX_X_OFFSET;
		internal int RightMargin { get; set; } = 20;
        internal int LeftMargin { get; set; } = 150;
        

		public int Width { get; set; }
        internal int Zoom { get; set; } = MAX_X_OFFSET;
        internal int Offset { get; set; } = 0; //Desplazamiento en segundos.
        public int EndOffset => Offset + Zoom; //Final de la zona representable en zoom.
        internal double DisplayOffsetXPx => -((Right - LeftMargin) * Offset) / Zoom;
        internal int Right => Width - RightMargin;
        internal int mvarGraphicalWidth => Width - LeftMargin - RightMargin;
        internal int mvarMaxOffset;

        internal TimeNetEnvironmentX(int width)
        {
            Width = width;
            LapseX = new TimeLapse { Begin = new TimeSpan(5, 30, 0), End = new TimeSpan(23, 59, 0) };
        }
        internal void ZoomIn()
        {
            Zoom = Math.Max(60, Zoom / 2);
            updateSliderParams();
        }
        internal void ZoomOut()
        {
            Zoom = Math.Min(MAX_X_OFFSET, Zoom * 2);
            updateSliderParams();
        }
        internal void OnScrollChanged(int position)
        {
            Offset = position;
        }
        internal double GetX(int seconds)
        {
            double ancho = Math.Abs(Right - LeftMargin);
            double salida = ancho * seconds / Zoom;
            return salida;
        }
        public TimeLapse LapseX
        {
            get => new TimeLapse { Begin = new TimeSpan(0, 0, Offset), End = new TimeSpan(0, 0, Offset + Zoom) };
            set
            {
                Offset = (int)value.Begin.TotalSeconds;
                Zoom = (int)value.Duration.TotalSeconds;
            }
        }
        internal void updateSliderParams()
        {
            mvarMaxOffset = Math.Max(0, MAX_X_OFFSET - Zoom);
            //mvarSliderStep = Math.Max(1, ZoomXSeconds / 10);

            if (Offset > mvarMaxOffset)
                Offset = mvarMaxOffset;
            if (Offset < 0)
                Offset = 0;
        }
    }
}
