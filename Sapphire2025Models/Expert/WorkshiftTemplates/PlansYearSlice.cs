using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2025Models.Expert.WorkshiftTemplates
{
    /// <summary>
    /// Modelo que almacena un conjunto de asignaciones consecutivas para un cierto número de días
    /// del año. Sirve para la representación en un gráfico de Agentes
    /// </summary>
    public class PlansYearSlice
    {
        public int mvarDayCount;
        public Dictionary<Guid, WorkShiftTemplateCollectionModel> mcolPlans; //Diccionario que contiene todas las referencias a los planes.
        public Guid[] mcolPlanPointers;
        public PlansYearSlice()
        {
            mcolPlans = new Dictionary<Guid, WorkShiftTemplateCollectionModel>();
            mcolPlanPointers = new Guid[0];
            ColFestives = new bool[0];
            DayCount = 0;
            InitialDate = DateTime.Today;
        }
        public PlansYearSlice(int days, DateTime dateBegin)
        {
            mcolPlans = new Dictionary<Guid, WorkShiftTemplateCollectionModel>();
            ColFestives = new bool[days];            
            mcolPlanPointers = new Guid[days];
            InitialDate = dateBegin;
            DayCount = days;
        }
        public DateTime InitialDate { get; set; } //Fecha inicial
        public DateTime FinalDate { get => InitialDate.AddDays(DayCount); }
        public int DayCount //Número de días
        {
            get => mvarDayCount;
            set
            {
                mvarDayCount = value;
                ColFestives = new bool[value];
                mcolPlanPointers = new Guid[value];
            }
        } 
        public WorkShiftTemplateCollectionModel[] ColPlans 
        { 
            get
            {
                WorkShiftTemplateCollectionModel[] salida = new WorkShiftTemplateCollectionModel[DayCount];
                for (int i=0;i< DayCount;i++)
                {
                    if (mcolPlans.ContainsKey(mcolPlanPointers[i]))
                        salida[i] = mcolPlans[mcolPlanPointers[i]];                        
                }
                return salida;
            }
        }
        public bool[] ColFestives { get; private set; }
        public bool SetFestive (DateTime day, bool Festive)
        {
            int offset = (int)day.Subtract(InitialDate).TotalDays;
            if (offset >= DayCount || offset < 0) return false;
            ColFestives[offset] = Festive;
            return true;
        }
        public bool GetFestive (DateTime day)
        {
            int offset = (int)day.Subtract(InitialDate).TotalDays;
            return GetFestive(offset);

        }
        public bool GetFestive(int dayId)
        {
            if (dayId >= DayCount || dayId < 0) return false;
            return ColFestives[dayId];
        }
        public DateTime GetDay(int dayId)
        {
            if (dayId >= DayCount)
                return InitialDate;
            else
                return InitialDate.AddDays(dayId);
        }
        public bool SetPlan(DateTime day,WorkShiftTemplateCollectionModel plan)
        {
            int offset = (int)day.Subtract(InitialDate).TotalDays;
            return SetPlan(offset, plan);
        }
        public bool SetPlan(int dayIndex, WorkShiftTemplateCollectionModel plan)
        {
            if (dayIndex >= DayCount || dayIndex < 0) return false;
            if (!mcolPlans.ContainsKey(plan.Id))
                mcolPlans.Add(plan.Id, plan);
            mcolPlanPointers[dayIndex] = plan.Id;
            return true;
        }
        public WorkShiftTemplateCollectionModel? GetPlan(DateTime day)
        {
            int offset = (int)day.Subtract(InitialDate).TotalDays;
            return GetPlan(offset);
        }
        public WorkShiftTemplateCollectionModel? GetPlan(int dayId)
        {
            if (dayId < DayCount && dayId >= 0)
            {
                if (mcolPlans.ContainsKey(mcolPlanPointers[dayId]))
                    return mcolPlans[mcolPlanPointers[dayId]];
            }
            return null;            
        }              
    }
}
