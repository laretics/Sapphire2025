using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimeNet2026.Storage;
using TimeNet2026.Timed;
using TimeNet2026.Topo;
using TimeNet2026.Production;

namespace TimeNetComponents.Controls.TimeNetControl
{
    /// <summary>
    /// Esta clase contiene los componentes mínimos que necesito para mostrar una malla horaria.
    /// </summary>
    public class TimeNetControlEnvironment
    {
		//Entorno de TimeNet asociado.
		public TimeNetEnvironment? Environment 
        {
            get => mvarEnvironment;
            set
            {
                mvarEnvironment = value;
                initView();
			}
        }
		public TimeNetEnvironmentX XX { get; private set; } //Coordenada X.
        public TimenetEnvironmentY YY { get; private set; } //Coordenada Y.
        private int mvarWidth;
        private int mvarHeight;
        private TimeNetEnvironment? mvarEnvironment = null;

		public TimeNetControlEnvironment(int width, int height)
        {
            mvarWidth = width;
            mvarHeight = height;

			initView();
            XX = new TimeNetEnvironmentX(width);			
            if(null!=this.Environment && null!= this.Environment.ViewAsimilation)
				YY = new TimenetEnvironmentY(height, Environment.TopoStorage, Environment.ViewAsimilation);
            else
				YY = new TimenetEnvironmentY(height, null, null);
		}
        //Esta función se invoca cada vez que ha cambiado algo importante en la malla.
        internal void initView()
        {
            XX = new TimeNetEnvironmentX(mvarWidth);
            if(null==this.Environment?.ViewAsimilation)
                YY = new TimenetEnvironmentY(mvarHeight, null, null);
            else
                YY = new TimenetEnvironmentY(mvarHeight, Environment.TopoStorage, Environment.ViewAsimilation);
		}

        internal bool IsViewComplete => null==Environment?false:Environment.IsViewComplete;
        internal string ViewError => null==Environment?"": Environment.ViewError;
    }
}
