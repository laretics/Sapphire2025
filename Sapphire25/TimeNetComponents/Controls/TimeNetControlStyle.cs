using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimeNet2026.Timed;

namespace TimeNetComponents.Controls
{
    /// <summary>
    /// Esta clase contiene los elementos de visualización de estilos comunes para la malla horaria
    /// y los controles de visualización Onice
    /// </summary>
    public class TimeNetControlStyle
    {
        public int Width { get; set; }
        public int Height { get; set; }
        internal int ZoomXSeconds { get; set; } = MAX_X_OFFSET;
        internal int OffsetXSeconds { get; set; } = 0; //Desplazamiento en segundos.
        public int EndOffsetXSeconds => OffsetXSeconds + ZoomXSeconds; //Final de la zona representable en zoom.
        private const int TOTAL_HOURS = 24;
        internal const int MAX_X_OFFSET = 3600 * TOTAL_HOURS;
        internal int MarginXRight { get; set; } = 20;
        internal int MarginXLeft { get; set; } = 150;
        internal double DisplayOffsetXPx => -((mvarMaxX - MarginXLeft) * OffsetXSeconds) / ZoomXSeconds;
        internal int mvarMaxX => Width - MarginXRight;
        internal int mvarGraphicalWidth => Width - MarginXLeft - MarginXRight;
        internal int mvarMaxOffset;

        internal ViewingTheme Theme { get; private set; }
        internal Dictionary<string, ViewingTheme> mcolThemes;
        
        public TimeLapse LapseX
        {
            get => new TimeLapse { Begin = new TimeSpan(0, 0, OffsetXSeconds), End = new TimeSpan(0, 0, OffsetXSeconds + ZoomXSeconds) };
            set
            {
                OffsetXSeconds = (int)value.Begin.TotalSeconds;
                ZoomXSeconds = (int)value.Duration.TotalSeconds;
            }
        }
        public TimeNetControlStyle(int width, int height)
        {
            mcolThemes = new Dictionary<string, ViewingTheme>();
            this.Width = width;
            this.Height = height;
            LapseX = new TimeLapse { Begin = new TimeSpan(5, 30, 0), End = new TimeSpan(23, 59, 0) };
            Init();
            this.Theme = mcolThemes["Day"];
        }

        internal double GetX(int seconds)
        {
            double ancho = Math.Abs(mvarMaxX - MarginXLeft);
            double salida = (ancho * seconds) / ZoomXSeconds;
            return salida;
        }
        internal void updateSliderParams()
        {
            mvarMaxOffset = Math.Max(0, MAX_X_OFFSET - ZoomXSeconds);
            //mvarSliderStep = Math.Max(1, ZoomXSeconds / 10);

            if (OffsetXSeconds > mvarMaxOffset)
                OffsetXSeconds = mvarMaxOffset;
            if (OffsetXSeconds < 0)
                OffsetXSeconds = 0;
        }
        internal void ZoomIn()
        {
            ZoomXSeconds = Math.Max(60, ZoomXSeconds / 2);
            updateSliderParams();
        }
        internal void ZoomOut()
        {
            ZoomXSeconds = Math.Min(MAX_X_OFFSET, ZoomXSeconds * 2);
            updateSliderParams();
        }
        internal void OnScrollChanged(int position)
        {
            OffsetXSeconds = position;
        }
        public void SwitchTheme(bool day)
        {
            SelectTheme(day ? "Day": "Night");
        }
        public void AddCustomTheme(ViewingTheme rhs)
        {
            //Si el nuevo tema tiene el mismo nombre que uno anterior, elimino el anterior.
            if (mcolThemes.ContainsKey(rhs.Name))
                mcolThemes.Remove(rhs.Name);

            mcolThemes.Add(rhs.Name, rhs);
            this.Theme = rhs;
        }
        public void SelectTheme(string key)
        {
            if (mcolThemes.ContainsKey(key))
                this.Theme = mcolThemes[key];
        }
        internal void Init()
        {
            ViewingTheme lightTheme = new ViewingTheme
            {
                Name = "Day",
                Variables = new()
                {
                    { "--bg-color", "linear-gradient(to bottom, #f8f9fa, #e9ecef)" },
                    { "--border-color", "#ccc" },
                    { "--station-line-color", "#aaa" },
                    { "--singular-line-color", "#eee" },
                    { "--circulation-label-color", "white" },
                    { "--hour-line-color", "black" },
                    { "--hour-line-thick-color", "#d1e7f0" },
                    { "--hour-line-thin-color", "#8fa3ad" },
                    { "--half-hour-line-color", "#b0c4cc" },
                    { "--quarter-hour-line-color", "#d1e7f0" },
                    { "--five-minute-line-color", "#e8f4f8" },
                    { "--singular-name-fill", "gray" },
                    { "--station-name-fill", "teal" },
                    { "--axis-stroke-color", "black" },
                    { "--hour-text-color", "black" },
                    { "--unselected-time-fill", "#00000090" },
                    { "--train-color-index", "0" }
                }
            };
            AddCustomTheme(lightTheme);
            ViewingTheme darkTheme = new ViewingTheme
            {
                Name = "Night",
                Variables = new()
                {
                    { "--bg-color", "linear-gradient(to bottom, #333, #555)" },
                    { "--border-color", "#666" },
                    { "--station-line-color", "#777" },
                    { "--singular-line-color", "#444" },
                    { "--circulation-label-color", "black" },
                    { "--hour-line-color", "white" },
                    { "--hour-line-thick-color", "#4a6c7b" },
                    { "--hour-line-thin-color", "#5d7a8a" },
                    { "--half-hour-line-color", "#6a8b9e" },
                    { "--quarter-hour-line-color", "#4a6c7b" },
                    { "--five-minute-line-color", "#3a5a6b" },
                    { "--singular-name-fill", "#ccc" },
                    { "--station-name-fill", "#add8e6" },
                    { "--axis-stroke-color", "white" },
                    { "--hour-text-color", "white" },
                    { "--unselected-time-fill", "#000000C0" },
                    { "--train-color-index", "1" }
                }
            };
            AddCustomTheme(darkTheme);
        }
    }
}
