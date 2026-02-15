using Microsoft.EntityFrameworkCore;
namespace TimeNet2026Data.DBStorage
{
	[Index(nameof(AxisId))]
	[Index(nameof(Pk))]	
	public class DBRefPunctual
	{		
		public int AxisId { get; set; } //Entero con la referencia del eje
		public long Pk { get; set; } //Posición en el eje
		public double Latitude { get; set; }
		public double Longitude { get; set; }
	}
}
