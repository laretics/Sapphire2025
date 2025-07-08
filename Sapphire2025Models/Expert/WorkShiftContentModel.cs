using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Sapphire2025Models.Expert
{
	[JsonPolymorphic(TypeDiscriminatorPropertyName ="$type")]
	[JsonDerivedType(typeof(AttWorkShiftContentModel),"atw")]
	[JsonDerivedType(typeof(TrainWorkShiftContentModel),"trw")]
	public abstract class WorkShiftContentModel
	{
		public TimeSpan StartTime { get; set; }
		public TimeSpan EndTime { get; set; }
		public TimeSpan Duration { get => EndTime.Subtract(StartTime); }
	}
	public class AttWorkShiftContentModel:WorkShiftContentModel
	{
		public bool Foreign { get; set; } //El Att se realiza fuera de la residencia principal
	}
	public class TrainWorkShiftContentModel : WorkShiftContentModel
	{
		public string? TrainId { get; set; }
		public bool Discrectional { get; set; } //Si es true, hacer este tren es opcional para el turno.
	}
}
