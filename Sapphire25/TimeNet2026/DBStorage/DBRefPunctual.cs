using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;

namespace TimeNet2026.DBStorage
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
