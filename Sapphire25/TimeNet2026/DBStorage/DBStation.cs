using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace TimeNet2026.DBStorage
{
	public class DBStation
	{
		[Key]
		public int Id { get; set; } //Autonumérico para la estación
		public string StationId { get; set; } = string.Empty; //Id según Onice.
		public int AxisId { get; set; } //Referencia en la tabla DBRefPuntual
		public long Pk { get; set; } //Es el mismo que en la tabla RefPunctual
		public string Name { get; set; } = string.Empty;
		public string ShortName { get; set; } = string.Empty;

	}
}
