using System.ComponentModel.DataAnnotations;
namespace TimeNet2026Data.DBStorage
{
	[MessagePack.MessagePackObject]
	public class DBSchedule
	{
		[System.ComponentModel.DataAnnotations.Key]
		[MessagePack.Key(0)]
		public int Id { get; set; } //Autonumérico para el turno de trabajo
		[MessagePack.Key(1)]
		public int PlanId { get; set; } //Referencia al plan de explotación
		[MessagePack.Key(2)]
		public string Name { get; set; } = string.Empty; //Nombre del turno de trabajo
		[MessagePack.Key(3)]
		public string Comment { get; set; } = string.Empty; //Comentarios del turno de trabajo
		[MessagePack.Key(4)]
		public byte WeekdayMask { get; set; } //Días de la semana en que está operativo este horario.
		[MessagePack.Key(5)]
		public int CoordinateX { get; set; } //Coordenada X en la presentación gráfica
		[MessagePack.Key(6)]
		public int CoordinateY { get; set; } //Coordenada Y en la presentación gráfica
		[MessagePack.Key(7)]
		public string Color1 { get; set; } = "white"; //Color primero del turno
		[MessagePack.Key(8)]
		public string Color2 { get; set; } = "black"; //Color segundo del turno


	}
}
