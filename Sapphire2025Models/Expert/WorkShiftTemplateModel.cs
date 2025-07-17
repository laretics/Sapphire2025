using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Sapphire2025Models.Expert
{
	//Cualquier asignación tiene estos datos.
	[JsonPolymorphic(TypeDiscriminatorPropertyName ="$type")]
	[JsonDerivedType(typeof(RestTemplateModel),"rst")]
	[JsonDerivedType(typeof(AttTemplateModel),"att")]
	[JsonDerivedType(typeof(WorkTemplateModel),"wkt")]
	public abstract class WorkShiftTemplateModel
	{		
		public string Name { get;set;}
		public string? comment { get; set; }
		public string? Color { get; set; }
		public string? BgColor { get; set; }
		public string? StripeColor { get; set; }
		public int CoorX { get; set; } //Lugar de representación en la tabla del gráfico
		public int CoorY { get; set; } //Coordenada y de representación en la tabla del gráfico
	}
	//Descanso, vacaciones y similar.
	public class RestTemplateModel:WorkShiftTemplateModel
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
