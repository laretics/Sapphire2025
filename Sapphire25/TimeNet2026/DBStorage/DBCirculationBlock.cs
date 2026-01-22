using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace TimeNet2026.DBStorage
{
	internal class DBCirculationBlock
	{
		[Key]
		public int Id { get; set; } //Código interno del bloque de circulaciones
		public int PlanId { get; set; } //Referencia al plan de explotación al que pertenece este bloque de circulaciones
		public string AsimilationId { get; set; } = string.Empty;//Código TimeNet de la asimilación        
		public byte WeekdayMask { get; set; }
		public string Pattern { get; set; } = string.Empty; //Patrón de numeración
	}
}
