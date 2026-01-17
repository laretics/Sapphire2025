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
        internal TimeNetEnvironment Environment {get;set; } //Entorno de TimeNet asociado.
		public TimeNetEnvironmentX XX { get; private set; } //Coordenada X.
        public TimenetEnvironmentY YY { get; private set; } //Coordenada Y.
              
        public TimeNetControlEnvironment(int width, int height, OnyxStorage storage, string? topoStorageId, string? viewId, string? rautaId, string? planId)
        {
            XX = new TimeNetEnvironmentX(width);			
            this.Environment = new TimeNetEnvironment(storage, topoStorageId, viewId, rautaId, planId);
            if(null!=this.Environment.ViewAsimilation)
				YY = new TimenetEnvironmentY(height, Environment.TopoStorage, Environment.ViewAsimilation);
            else
				YY = new TimenetEnvironmentY(height, null, null);
		}
        internal bool IsViewComplete => Environment.IsViewComplete;
        internal string ViewError => Environment.ViewError;
    }
}
