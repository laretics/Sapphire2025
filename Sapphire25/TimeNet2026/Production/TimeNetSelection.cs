using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimeNet2026.Timed;

namespace TimeNet2026.Production
{
    /// <summary>
    /// Esta clase contiene los datos sobre la selección de espacio, tiempo o circulaciones.
    /// </summary>
    public class TimeNetSelection
    {
        internal List<string> mcolCirculations; //Índices de las circulaciones seleccionadas
        internal List<string> mcolHighCirculations; //Índices de las circulaciones especiales seleccionadas
        internal TimeLapseCollection mcolTimeSelection; //Regiones temporales seleccionadas
        public IEnumerable<string> Circulations => mcolCirculations;
        public IEnumerable<string> HCirculations => mcolHighCirculations;
        
        public TimeLapseCollection XSelection => mcolTimeSelection;        

        public TimeNetSelection()
        {
            mcolCirculations = new List<string>();
            mcolHighCirculations = new List<string>();
            mcolTimeSelection = new TimeLapseCollection();

            mcolCirculations.Add("4544");
            mcolHighCirculations.Add("4928");

            mcolTimeSelection.Add(new TimeLapse { Begin = new TimeSpan(5, 30, 0), End = new TimeSpan(23, 00, 0) });
        }

    }
}
