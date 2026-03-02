using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Schema;
using TimeNet2026.Storage;
using TimeNet2026.Timed;
using TimeNet2026.Topo;

namespace TimeNet2026.Production
{
    /// <summary>
    /// Esta clase contiene los componentes mínimos que necesito para mostrar una malla horaria.
    /// </summary>
    public class TimeNetEnvironment
    {
        public OnyxStorage OnyxStorage { get; private set; }
        public TopoStorage? TopoStorage { get; set; }
        public TopoAxis? Axis { get; set; } //El eje se necesita para los nodos de los árboles.
		public Asimilation? ViewAsimilation { get; set; } //Asimilación que marca la vista de la malla.
        public Asimilation? Asimilation { get; set; }//Asimilación a mostrar o asimilación vigente.
        public Rauta? Rauta { get; set; } //Almacén donde están los planes
        public Plan? Plan { get; set; } //Plan donde están los trenes que hay que visualizar.
        public CirculationBlock? CirculationBlock { get; set; } //Bloque de circulaciones que se está editando.
		public Circulation? Circulation { get; set; } //Circulación que se está editando.
		public TimeNetEnvironment(TimeNetEnvironment original) 
        { 
            this.OnyxStorage = original.OnyxStorage;
            this.TopoStorage = original.TopoStorage;
            this.Axis = original.Axis;
            this.ViewAsimilation = original.ViewAsimilation;
            this.Asimilation = original.Asimilation;
			this.Rauta = original.Rauta;
            this.Plan = original.Plan;
			this.CirculationBlock = original.CirculationBlock;
			this.Circulation = original.Circulation;            
		}
        public TimeNetEnvironment(OnyxStorage storage)
        {
            this.OnyxStorage = storage;
		}
		public TimeNetEnvironment(OnyxStorage storage, string? topoStorageId = null, string? viewId = null, string? rautaId = null, string? planId = null):this(storage)
		{
			this.TopoStorageId = topoStorageId ?? string.Empty;
			this.ViewId = viewId ?? string.Empty;
			this.RautaId = rautaId ?? string.Empty;
			this.PlanId = planId ?? string.Empty;
		}
        public TimeNetEnvironment(OnyxStorage storage, 
            TopoStorage topoStorage , 
            TopoAxis? axis = null, 
            Asimilation? viewAsimilation = null, 
            Rauta? rauta = null, 
            Plan? plan = null):this(storage)
        {
            this.TopoStorage = topoStorage;
            this.Axis = axis;
            this.ViewAsimilation = viewAsimilation;
            this.Rauta = rauta;
            this.Plan = plan;
        }
		public string? TopoStorageId 
        {
            get => null==TopoStorage? null: TopoStorage.Header.Id.ToString();
            set
            {
                if(null!=value)
                {
					Guid auxTopoStorageId = Guid.Empty;
					if (Guid.TryParse(value, out auxTopoStorageId))
					{
						if (OnyxStorage.Storages.ContainsKey(auxTopoStorageId))
							this.TopoStorage = OnyxStorage.Storages[auxTopoStorageId];
					}
				}
			}
        }
        public string? ViewId 
        {
            get => null==ViewAsimilation ? null: ViewAsimilation.id;
            set
            {
                if (null != value &&  null!=TopoStorage)
                {
                    if(TopoStorage.ColAsimilations.ContainsKey(value))
                        ViewAsimilation = TopoStorage.ColAsimilations[value];
                }
            }
        }
        public string? RautaId 
        {
            get => null == Rauta ? null : Rauta.Header.Id.ToString();
            set
            {
                if (null != value && null != TopoStorage)
                {
                    Guid auxRautaId = Guid.Empty;
                    if (Guid.TryParse(value, out auxRautaId))
                    {
                        if (TopoStorage.ColRauta.ContainsKey(auxRautaId))
                            Rauta = TopoStorage.ColRauta[auxRautaId];
                    }
                }
            }
        }
        public string? PlanId 
        {
            get => null == Plan ? null : Plan.Id;
            set
            {
                if(null != value && null != Rauta)
                {
                    if(Rauta.Plans.ContainsKey(value))
                        Plan = Rauta.Plans[value];
                }
            }
        }
            
        /// <summary>
        /// Devuelve el lapso de tiempo que define la selección de la vista.
        /// </summary>
        /// <returns></returns>
        public TimeLapseCollection ViewLapse()
        {
            //Por defecto, el lapso de la vista es todo el lapso de representación.
            TimeLapseCollection salida = new TimeLapseCollection();
            if (null != Plan && null != Rauta)
            {
                if (null != Circulation)
                {
                    salida.Add(Circulation.TimeLapse);
                }
                if (null != CirculationBlock)
                { 
                    foreach (Circulation circ in CirculationBlock.Circulations)
                        salida.Add(circ.TimeLapse);
                }
                else if (null != Asimilation)
                {
                    foreach (CirculationBlock block in Plan.CirculationBlocks)
                    {
                        if (block.asimilation == Asimilation)
                        {
                            foreach (Circulation circ in block.Circulations)
                                salida.Add(circ.TimeLapse);
                        }
                    }
                }
                else
                    salida = Plan.TotalTimeLapse;
			}           
                return salida;
		}

		public bool IsViewComplete => null != TopoStorage && null != ViewAsimilation && null != Rauta && null != Plan;
        public string ViewError
        {
            get
            {
                if (IsViewComplete) return "Todo está correcto";
                StringBuilder salida = new StringBuilder();
                salida.Append("Falta asignar valor a");
                if (null != TopoStorage) salida.Append(" la topología");
                if (null != ViewAsimilation) salida.Append(" la asimilación que define la vista");
                if (null != Rauta) salida.Append("l componente de horarios");
                if (null != Plan) salida.Append("l plan de asimilación");
                return salida.ToString();
            }
        }
    }
}
