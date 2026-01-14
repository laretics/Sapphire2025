using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimeNet2026.Timed;

namespace TimeNetComponents.Controls
{
    /// <summary>
    /// Esta clase contiene los datos sobre la selección de espacio, tiempo o circulaciones.
    /// </summary>
    public class TimeNetControlSelection
    {
        internal List<string> mcolCirculations; //Índices de las circulaciones seleccionadas
        internal TimeLapseCollection mcolTimeSelection; //Regiones temporales seleccionadas
        public IEnumerable<string> Circulations => mcolCirculations;
        public TimeLapseCollection XSelection => mcolTimeSelection;        

        public TimeNetControlSelection()
        {
            mcolCirculations = new List<string>();
            mcolTimeSelection = new TimeLapseCollection();

            mcolTimeSelection.Add(new TimeLapse { Begin = new TimeSpan(5, 10, 0), End = new TimeSpan(6, 30, 0) });
            mcolTimeSelection.Add(new TimeLapse { Begin = new TimeSpan(9, 00, 0), End = new TimeSpan(11, 00, 0) });
            mcolTimeSelection.Add(new TimeLapse { Begin = new TimeSpan(21, 00, 0), End = new TimeSpan(23, 43, 0) });
        }

    }
}
