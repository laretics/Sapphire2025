using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2025Models.GMao
{
	public class WorkOrderModel
	{
		public Guid Id { get; set; } //Código único de esta orden de trabajo
		public Guid WorkType { get; set; } //Tipo de trabajo de la orden de trabajo
		public Guid? DestinationObjectId { get; set; } //Elemento del tren sobre el que se aplica esta orden de trabajo
		public Guid? TrainId { get; set; } //Tren sobre el que se aplica esta orden en el momento de su apertura.
										   //Es nullable porque se podría hacer un trabajo sobre una pieza de parque.
		public Guid OpenUserId { get; set; } //Usuario del sistema que abre la orden de trabajo
		public Guid? CloseUserId { get; set; } //Usuario del sistema que cierra la orden de trabajo
		public Guid? VerifyUserId { get; set; } //Usuario del sistema que verifica el trabajo realizado.
		public DateTime OpenTime { get; set; } //Fecha de apertura de la orden
		public DateTime? CloseTime { get; set; } //Fecha de cierre de la orden
		public DateTime? VerifyTime{ get; set; } //Fecha de verificación
	}
}
