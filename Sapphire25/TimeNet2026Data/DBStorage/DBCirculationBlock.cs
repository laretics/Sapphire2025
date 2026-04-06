using System.ComponentModel.DataAnnotations;
namespace TimeNet2026Data.DBStorage
{
	[MessagePack.MessagePackObject]
	public class DBCirculationBlock
	{
		[MessagePack.Key(0)]
		[System.ComponentModel.DataAnnotations.Key]
		public int Id { get; set; } //Código interno del bloque de circulaciones
		[MessagePack.Key(1)]
		public int PlanId { get; set; } //Referencia al plan de explotación al que pertenece este bloque de circulaciones
		[MessagePack.Key(2)]
		public string AsimilationId { get; set; } = string.Empty;//Código TimeNet de la asimilación        
		[MessagePack.Key(3)]
		public byte WeekdayMask { get; set; }
		[MessagePack.Key(4)]
		public string Pattern { get; set; } = string.Empty; //Patrón de numeración
	}
}
