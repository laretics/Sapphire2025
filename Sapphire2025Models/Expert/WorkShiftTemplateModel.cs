using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2025Models.Expert
{
	//Cualquier asignación tiene estos datos.
	public abstract class WorkShiftTemplateModel
	{
		public string Name { get;set;}
		public string? comment { get; set; }
		public string? Color { get; set; }
	}
	//Descanso, vacaciones y similar.
	public class VacationTemplateModel:WorkShiftTemplateModel
	{

	}	
	//Turno de trabajo con un horario.
	public class AttTemplateModel:WorkShiftTemplateModel
	{
		public TimeSpan StartTime { get; set; }
		public TimeSpan Duration { get; set; }
		public TimeSpan EndTime { get => StartTime.Add(Duration); set => Duration = value.Subtract(StartTime); }
	}
	public class WorkTemplateModel:AttTemplateModel
	{
		public List<WorkShiftContentModel>? Content { get; set; } //Trenes y depósitos que contiene este turno.
	}


}
