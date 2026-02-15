using System.ComponentModel.DataAnnotations;

namespace TimeNet2026Data.DBStorage
{
	public class DBScheduleUnit
	{
		[Key]
		public int Id { get; set; } //Autonumérico para el ítem del turno de trabajo
		public int ScheduleId { get; set; } //Referencia al turno de trabajo
		public int CirculationId { get; set; } //Referencia a la circulación
		public bool Active { get; set; } //Indica si el maquinista trabaja en esta parte
		public TimeSpan Begin { get; set; } //Hora de inicio de la actividad
		public TimeSpan End { get; set; } //Hora de fin de la actividad
	}
}
