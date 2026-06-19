using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Sapphire2026Data.Models.GMAO
{
	/// <summary>
	/// Orden de trabajo sobre un tren.
	/// </summary>
	[Table("GMAOWorkOrders")]
	public class WorkOrder
	{
		[Key]
		public Guid Id{ get; set; } //Código único de esta orden de trabajo
		public Guid WorkType { get; set; } //Tipo de trabajo de la orden de trabajo
		public Guid? DestinationObjectId{ get; set; } //Elemento del tren sobre el que se aplica esta orden de trabajo
		public Guid? TrainId{ get; set; } //Tren sobre el que se aplica esta orden en el momento de su apertura.
		//Es nullable porque se podría hacer un trabajo sobre una pieza de parque.
		public Guid OpenUserId{ get; set; } //Usuario del sistema que abre la orden de trabajo
		public Guid? CloseUserId{ get; set; } //Usuario del sistema que cierra la orden de trabajo
		public Guid? VerifyUserId{ get; set; } //Usuario del sistema que verifica el trabajo realizado.
		public DateTime OpenTime{ get; set; } //Fecha de apertura de la orden
		public DateTime? CloseTime{ get; set; } //Fecha de cierre de la orden

	}
}
