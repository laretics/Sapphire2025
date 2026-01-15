using System;
using System.Collections.Generic;
using System.Drawing;
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
 

        internal ViewingTheme Theme { get; private set; }
        internal Dictionary<string, ViewingTheme> mcolThemes;
        public bool CurrentTime { get; set; }
        public bool ShowLabelsInSelected { get; set; } //Muestra las etiquetas sólo en los trenes seleccionados
        public bool ShowLabelsInHighlighted { get; set; } //Muestra las etiquetas sólo en los trenes principales

        public TimeNetControlStyle()
        {
            mcolThemes = new Dictionary<string, ViewingTheme>();
            CurrentTime = true;
            Init();
            this.Theme = mcolThemes["Day"];
            ShowLabelsInHighlighted = true;
            ShowLabelsInSelected = true;
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
                    { "--current-time-color-front","#ee2222" },
                    { "--current-time-color-fill","#ee4444" },
                    { "--train-color-index", "0" }
                }
            };
            AddCustomTheme(lightTheme);
            ViewingTheme darkTheme = new ViewingTheme
            {
                Name = "Night",
                Variables = new()
                {
                    { "--bg-color", "linear-gradient(to bottom, #111, #333)" },
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
                    { "--current-time-color-front","#ffaaaa" },
                    { "--current-time-color-fill","#ff8888" },
                    { "--train-color-index", "1" }
                }
            };
            AddCustomTheme(darkTheme);
        }
    }
}
