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
			ResetSelection();
            //mcolHighCirculations.Add("4928");

        }
        public void ResetSelection()
        {
            mcolCirculations.Clear();
            mcolHighCirculations.Clear();
            mcolTimeSelection = new TimeLapseCollection();
        }
		public void SelectAsimilation(TimeNetEnvironment enviro, Asimilation? asimilation)
        {
            ResetSelection();
            if(null!=enviro.TopoStorage && null!=enviro.Rauta && null!=enviro.Plan && null!=asimilation)
            {
                foreach(CirculationBlock circulationBlock in enviro.Plan.CirculationBlocks)
                {
                    if(circulationBlock.asimilation == asimilation)
                    {
						foreach (Circulation circulation in circulationBlock.Circulations)
							mcolCirculations.Add(circulation.name);
					}
				}
                mcolTimeSelection = enviro.ViewLapse();
			}
		}
        public void SelectCirculationBlock(TimeNetEnvironment enviro, CirculationBlock? circulationBlock)
        {
            ResetSelection();
            if (null != enviro.TopoStorage && null != enviro.Rauta && null != enviro.Plan && null!=circulationBlock)
            {
                foreach (Circulation circulation in circulationBlock.Circulations)
					mcolCirculations.Add(circulation.name);
				mcolTimeSelection = enviro.ViewLapse();
			}
        }
        public void SelectCirculation(TimeNetEnvironment enviro, Circulation? circulation)
        {
            ResetSelection();
            if (null != enviro.TopoStorage && null != enviro.Rauta && null != enviro.Plan && null!=circulation)
				mcolCirculations.Add(circulation.name);

			mcolTimeSelection = enviro.ViewLapse();			
		}
	}
}
