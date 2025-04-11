using Sapphire2025Models;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Sapphire2025Server.Models
{
	[Table("StatusChanges")]
	public class StatusChange
	{		
		public StatusChange()
		{
			UserId = string.Empty;
		}
		[Key]
		public Guid Guid { get; set; } //Referencia interna de esta transacción
		public Guid TrainId { get; set; } //Referencia a la unidad que cambia de estado
		public DateTime TimeStamp { get; set; } //Fecha y hora del cambio de estado
		public byte mvarOperationId { get; set; } //Código de estado al que cambia el tren
		[NotMapped]
		public Common.OperationType Operation
		{
			get => (Common.OperationType)mvarOperationId;
			set => mvarOperationId = (byte)value;
		}
		[NotMapped]
		public Common.TrainStatus Status
		{
			get
			{
				switch (Operation)
				{
					//Ciclo de apartado, inicio y terminación (sólo administrador)
					case Common.OperationType.Activate: //Activación
					case Common.OperationType.RescueFromStandStill: //Rescate de parada de larga duración
						return Common.TrainStatus.RequestToRepair; // -->Solicitado para correctivo (Entrada en ciclo normal)
					case Common.OperationType.RescueFromDisabled: // -->Rescate de tren apartado de servicio
						return Common.TrainStatus.StandStill; // -->StandStill
					case Common.OperationType.SendToDisabled: //Baja del tren (desde standstill)
						return Common.TrainStatus.Disabled; //-->Disabled

					//Ciclo de correctivos
					case Common.OperationType.CorrectiveRequest: //Parte de averías
						return Common.TrainStatus.RequestToDiagnose; //Solicitud de diagnóstico
					case Common.OperationType.DiagnoseToFault: //Diagnóstico de avería
						return Common.TrainStatus.RequestToRepair; // -->Solicitado para correctivo
					case Common.OperationType.DiagnoseToAvailable: //Tren disponible (con o sin restricciones)
						return Common.TrainStatus.Available; //-->Retorno al servicio
					case Common.OperationType.BeginCorrective: //Inicio de correctivo
						return Common.TrainStatus.Repairing; // -->Inicio de correctivo
					case Common.OperationType.EndCorrective: //Fin de la reparación
						return Common.TrainStatus.Available; //-->Retorno al servicio


					//Ciclo de preventivos
					case Common.OperationType.DepotRequest: //Tren solicitado por el planificador
						return Common.TrainStatus.DepotRequested; //--> Solicitado por los mecánicos
					case Common.OperationType.DepotRequestDeny: //Un administrador deniega la petición
						return Common.TrainStatus.Available; //--> Tren pasa a estar disponible
					case Common.OperationType.MaintenanceRescue: //El inspector rescata un tren que iba para mantenimiento
						return Common.TrainStatus.DepotRequested; //--> Solicitado por los mecánicos
					case Common.OperationType.DepotRequestAccept: //Autorizado para mantenimiento por el inspector
						return Common.TrainStatus.DepotAvailable; //--> Disponible para el mantenimiento
					case Common.OperationType.BeginMaintenance: //Los mecánicos entran un tren para preventivo
						return Common.TrainStatus.Maintenance; //--> Tren apartado en taller para preventivo
					case Common.OperationType.DiferMaintenance: //Pausa en el preventivo hasta volver a circular
						return Common.TrainStatus.DepotAvailable; //--> Disponible para el mantenimiento
					case Common.OperationType.EndMaintenance: //Termina el mantenimiento
						return Common.TrainStatus.Available; //-->Retorno al servicio

					default:
						return Common.TrainStatus.Unknown;
				}
			}
		}
		public string UserId { get; set; } // Usuario que realiza esta transacción
	}
}
