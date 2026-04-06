using Microsoft.EntityFrameworkCore;
namespace TimeNet2026Data.DBStorage
{
	[Index(nameof(AxisId))]
	[Index(nameof(Pk))]
	[MessagePack.MessagePackObject]
	public class DBRefPunctual
	{
		[MessagePack.Key(0)]
		public int AxisId { get; set; } //Entero con la referencia del eje
		[MessagePack.Key(1)]
		public long Pk { get; set; } //Posición en el eje
		[MessagePack.Key(2)]
		public double Latitude { get; set; }
		[MessagePack.Key(3)]
		public double Longitude { get; set; }
	}
}
