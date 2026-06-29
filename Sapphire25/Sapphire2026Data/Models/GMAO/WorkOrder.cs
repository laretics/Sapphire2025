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
		public bool Atomic { get; set; } //Este trabajo es atómico (lo coge del catálogo)
		public bool Rejected{  get; set; } //El trabajo es rechazado (tras solicitud).
		public Guid? DestinationObjectId{ get; set; } //Elemento del tren sobre el que se aplica esta orden de trabajo
		public Guid? TrainId{ get; set; } //Tren sobre el que se aplica esta orden en el momento de su apertura.
		//Es nullable porque se podría hacer un trabajo sobre una pieza de parque.
		public Guid RequestUserId{ get; set; } //Usuario del sistema que solicita el trabajo		
		public Guid? OpenUserId{ get; set; } //Usuario del sistema que abre la orden de trabajo
		public Guid? CloseUserId{ get; set; } //Usuario del sistema que cierra la orden de trabajo
		public Guid? VerifyUserId{ get; set; } //Usuario del sistema que verifica el trabajo realizado.
		public DateTime RequestTime { get; set; } //Momento de la petición del trabajo
		public DateTime? OpenTime{ get; set; } //Fecha de apertura de la orden
		public DateTime? CloseTime{ get; set; } //Fecha de cierre de la orden
		public DateTime? VerifyTime { get; set; } //Fecha de verificación
	}
}
/*
 * 	 Órdenes de trabajo: Son comunes a lavados de tren, campañas y revisiones.
 * 	 
 * 	 Método (caso positivo):
 * 	 -1- Solicitud: Un mecánico solicita a la empresa la realización del trabajo.
 * 	 -2- Apertura: Un Gestor, Inspector o superior, autoriza el trabajo, que queda abierto.
 * 	 -3- Cierre: Un mecánico cierra el trabajo.
 * 	 -4- Verificación: Cualquier usuario valida el trabajo (opcional).
 * 	 
 * 	 Método (caso negativo):
 * 	 -1- Solicitud: Un mecánico solicita a la empresa la realización del trabajo. 
 * 	 -2- Denegación: Un Gestor, Inspector o superior, deniega el trabajo. Queda cerrado.
 * 		En este caso, Rejected := true, VerifyUserId := usuario que deniega, 
 * 		VerifyTime := hora de denegación.
 * */
