using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace TimeNet2026.DBStorage
{
	internal class DBAsimilationStep
	{
		[Key]
		public int Id { get; set; } //Autoincremental, sólo para consistencia de base de datos.
		public int AsimilationId { get; set; } //Id de la asimilación (de SQLite)
		public int DestinationStationId { get; set; } //Id de la estación (de SQLite)
		public int AxisId { get; set; } //Id interno del eje (de SQLite)
		public TimeSpan tripTime { get; set; }
		public TimeSpan stopTime { get; set; }
	}
}
