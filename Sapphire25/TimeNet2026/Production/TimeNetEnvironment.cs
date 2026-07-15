using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
using System;
using System.Collections.Generic;
using System.Data;
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
        private long mvarPreviousPkLocation = -1;
        private long mvarPk;
        public OnyxStorage OnyxStorage { get; private set; }
        public TopoStorage? TopoStorage { get; set; }
		public Rauta? Rauta { get; set; } //Almacén donde están los planes
		public Axis? Axis { get; set; } //El eje se necesita para los nodos de los árboles.
        public long PK //Punto kilométrico donde tengo ahora ubicado al tren.
        { 
            get => mvarPk; 
            set
            {
                mvarPk = value;
                auxUpdateRoute();
            }
        } 
        private void auxUpdateRoute()
        {
            if (Math.Abs(mvarPk - mvarPreviousPkLocation) > 500)
            {
                //Actualiza el sentido de la marcha en función de la lectura del GPS
                PKIncreasing = (mvarPreviousPkLocation < mvarPk);
                mvarPreviousPkLocation = mvarPk;
            }
            //Carga la asimilación residual.
            if(null!=Asimilation && null!=Axis)
                RouteAsimilation = Asimilation.SubAsimilation(Axis, mvarPk);
        }
        public bool PKIncreasing { get; set; } = true; //Orientación de la marcha del tren (Increasing avanza hacia un PK mayor)
        public Asimilation? ViewAsimilation { get; set; } //Asimilación que marca la vista de la malla.
        public Asimilation? Asimilation { get; set; }//Asimilación a mostrar o asimilación vigente.        
        public Asimilation? RouteAsimilation { get; set; } //Próximas estaciones
        public Plan? Plan { get; set; } //Plan donde están los trenes que hay que visualizar.
        public CirculationBlock? CirculationBlock { get; set; } //Bloque de circulaciones que se está editando.
        internal Circulation? mvarCirculation;
		//Buscamos una asimilación cualquiera que contenga el eje en el que estamos.
        //Necesito esta función para mostrar la vista de itinerario incluso cuando 
        //todavía no haya introducido la misión.
        public void SetAsimilationByAxis()
        {
            if(null!=Axis && null!=TopoStorage)
            {
                foreach(Asimilation auxAsimila in TopoStorage.ColAsimilations.Values)
                {
                    if (auxAsimila.containsAxis(Axis, PK) >= 0)
                    {
                        Asimilation = auxAsimila;
                        break;
                    }                        
                }
            }
        }        
        public Circulation? Circulation //Circulación que se está editando.
		{
            get => mvarCirculation;
            set
            {
                mvarCirculation = value;
                CirculationBlock = null;
                Asimilation = null;
                if(null!=mvarCirculation && null!=mvarCirculation.Parent)
                {
                    CirculationBlock = mvarCirculation.Parent;
                    if(null!=CirculationBlock.asimilation)
						Asimilation = CirculationBlock.asimilation;
                }
                ViewAsimilation = Asimilation;
            }
        }
        public TimeSpan CurrentDelay { get; set; } = new TimeSpan(0);
        public Weekday Weekday { get; set; } //Filtro para seleccionar trenes por días
        public DateTime Now{ get; set; } = DateTime.Now; //Hora actual, para sincronizar la malla con la información al viajero.
		public void SetWeekDate()
		{
            Weekday = GetWeekDay(Now);
		}
        public static Weekday GetWeekDay(DateTime rhs)
        {
			switch (rhs.DayOfWeek)
			{
				case DayOfWeek.Monday: return Weekday.Monday;
				case DayOfWeek.Tuesday: return Weekday.Tuesday;
				case DayOfWeek.Wednesday: return Weekday.Wednesday;
				case DayOfWeek.Thursday: return Weekday.Thursday;
				case DayOfWeek.Friday: return Weekday.Friday;
				case DayOfWeek.Saturday: return Weekday.Saturday;
				case DayOfWeek.Sunday: return Weekday.Sunday;
                default: return Weekday.None;
			}
		}
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
        /// <summary>
        /// Iniciador para Tourmaline
        /// </summary>
        /// <param name="storage"></param>
        /// <param name="TopoStorageId"></param>
        /// <param name="RautaId"></param>
        public TimeNetEnvironment(OnyxStorage storage, Guid TopoStorageId, Guid RautaId, string planName="")
        {
            this.OnyxStorage = storage;
            this.TopoStorageGuid = TopoStorageId;
            this.RautaGuid = RautaId;
            //Cargo un plan por defecto (el primero de la colección)
            //Esto permite que tenga un plan, aunque no lo haya especificado.
            if (null != Rauta && Rauta.Plans.Any())
                this.Plan = Rauta.Plans.Values.FirstOrDefault();
            if (planName.Length>0)
                this.PlanName = planName;
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
            Axis? axis = null, 
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

        public Guid TopoStorageGuid
        {
            get => null == TopoStorage ? Guid.Empty : TopoStorage.Header.Id;
            set
            {
                if (Guid.Empty != value)
                {
                    if(OnyxStorage.Storages.ContainsKey(value))
                        this.TopoStorage = OnyxStorage.Storages[value];
                }
            }
        }
		public string? TopoStorageId 
        {
            get => TopoStorageGuid.ToString();
            set
            {
                if(null!=value)
                {
					Guid auxTopoStorageId = Guid.Empty;
					if (Guid.TryParse(value, out auxTopoStorageId))
						TopoStorageGuid = auxTopoStorageId;
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
        public Guid RautaGuid
        {
            get => null == Rauta ? Guid.Empty : Rauta.Header.Id;
            set
            {
                if(Guid.Empty != value && null!=TopoStorage)
                {
                    if (TopoStorage.ColRauta.ContainsKey(value))
                        Rauta = TopoStorage.ColRauta[value];
				}                                   
            }
        }
        public string? RautaId 
        {
            get => RautaGuid.ToString();
            set
            {
				Guid auxRautaId = Guid.Empty;
				if (Guid.TryParse(value, out auxRautaId))
                    RautaGuid = auxRautaId;
            }
        }
        public string? PlanId 
        {
            get => null == Plan ? null : Plan.Id;
            set
            {
                if(null != value && null != Rauta)
					Plan = Rauta.Plans[value];
            }
        }
        public string? PlanName
        {
            get => null == Plan ? null : Plan.Name;
            set
            {
                if (null != value && null != Rauta)
                {
					Plan = Rauta.PlanByName(value);
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
                    foreach (CirculationBlock block in Plan.CirculationBlocksByDay)
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
