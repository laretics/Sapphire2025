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
		public Guid trainId { get; set; } //Referencia al tren sobre el que se ejecuta esta transacción
		public Guid userId { get; set; } //Referencia al usuario que ha ordenado la transacción
		public string? comment { get; set; }
	}
}
