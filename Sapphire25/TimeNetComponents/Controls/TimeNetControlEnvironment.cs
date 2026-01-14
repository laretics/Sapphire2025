using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimeNet2026.Topo;
using TimeNet2026.Storage;
using TimeNet2026.Timed;

namespace TimeNetComponents.Controls
{
    /// <summary>
    /// Esta clase contiene los componentes mínimos que necesito para mostrar una malla horaria.
    /// </summary>
    public class TimeNetControlEnvironment
    {
        internal TopoStorage? TopoStorage { get; set; }
        internal Asimilation? ViewAsimilation { get; set; } //Asimilación que marca la vista de la malla.     
        internal Rauta? Rauta { get; set; } //Almacén donde están los planes
        internal Plan? Plan { get; set; } //Plan donde están los trenes que hay que visualizar.
        
        public TimeNetControlEnvironment(OnyxStorage storage, string? topoStorageId, string? viewId, string? rautaId, string? planId)
        {
            if(null!=topoStorageId)
            {
                Guid auxTopoStorageId = Guid.Empty;
                Guid auxRautaId = Guid.Empty;
                if(Guid.TryParse(topoStorageId, out auxTopoStorageId))
                {
                    if(storage.Storages.ContainsKey(auxTopoStorageId))
                    {
                        TopoStorage = storage.Storages[auxTopoStorageId];
                        if(null!=viewId)
                        {
                            if (TopoStorage.ColAsimilations.ContainsKey(viewId))
                            {
                                ViewAsimilation = TopoStorage.ColAsimilations[viewId];
                                if (Guid.TryParse(rautaId, out auxRautaId))
                                {
                                    if(TopoStorage.ColRauta.ContainsKey(auxRautaId))
                                    {
                                        Rauta = TopoStorage.ColRauta[auxRautaId];
                                        if(null!=planId && Rauta.Plans.ContainsKey(planId))
                                        {
                                            Plan = Rauta.Plans[planId];
                                        }
                                    }    
                                }
                            }
                        }
                    }
                }

            }
        }
        internal bool IsViewComplete => null != TopoStorage && null != ViewAsimilation && null != Rauta && null != Plan;
        internal string ViewError
        {
            get
            {
                if (IsViewComplete) return "Todo está correcto";
                StringBuilder salida = new StringBuilder();
                salida.Append("Falta asignar valor a :");
                if (null != TopoStorage) salida.Append(" la base de datos de TimeNet");
                if (null != ViewAsimilation) salida.Append(" la asimilación que define la vista");
                if (null != Rauta) salida.Append(" el componente de horarios");
                if (null != Plan) salida.Append(" el plan de asimilación");
                return salida.ToString();
            }
        }
    }
}
