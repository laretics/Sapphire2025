using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2025Models.Aeneas
{
	public class StatusChangeModel
	{
		public Common.TrainStatus status { get; set; }
		public DateTime timeStamp { get; set; }
		public Guid guid { get; set; } //Referencia interna de la transacción
		public string? userId { get; set; } //Referencia al usuario que ha ordenado la transacción
		public string? comment { get; set; }
	}
}
