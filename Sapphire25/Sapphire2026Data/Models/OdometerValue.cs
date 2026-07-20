using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sapphire2026Data.Models
{
	public class OdometerValue
	{
		[Key]
		public long InternalId{ get; set; } //Autonumérico para la medida
		public DateTime TimeStamp{ get; set; } //Fecha y hora de la lectura
		public long Odometer{ get; set; } //Contador total del odómetro
	}
}
