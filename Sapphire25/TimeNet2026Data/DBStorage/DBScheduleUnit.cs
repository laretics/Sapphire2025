using System.ComponentModel.DataAnnotations;

namespace TimeNet2026Data.DBStorage
{
	[MessagePack.MessagePackObject]
	public class DBScheduleUnit
	{
		[System.ComponentModel.DataAnnotations.Key]
		[MessagePack.Key(0)]
		public int Id { get; set; } //Autonumérico para el ítem del turno de trabajo
		[MessagePack.Key(1)]
		public int ScheduleId { get; set; } //Referencia al turno de trabajo
		[MessagePack.Key(2)]
		public int CirculationId { get; set; } //Referencia a la circulación
		[MessagePack.Key(3)]
		public bool Active { get; set; } //Indica si el maquinista trabaja en esta parte
		[MessagePack.Key(4)]
		public TimeSpan Begin { get; set; } //Hora de inicio de la actividad
		[MessagePack.Key(5)]
		public TimeSpan End { get; set; } //Hora de fin de la actividad
	}
}
