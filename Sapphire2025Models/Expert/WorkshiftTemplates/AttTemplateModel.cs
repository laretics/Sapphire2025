using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2025Models.Expert.WorkshiftTemplates
{
    //Turno de trabajo con un horario.
    public class AttTemplateModel : WorkShiftTemplateModel
    {
        public TimeSpan StartTime { get; set; }
        public TimeSpan Duration { get; set; }
        public TimeSpan EndTime { get => StartTime.Add(Duration); set => Duration = value.Subtract(StartTime); }
		public List<WorkShiftContentModel>? Content { get; set; } //Trenes y depósitos que contiene este turno.
	}
}
