using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2025Models
{
	public static class Common
	{
		public const string SapphireSoftwareVersion = "26.02.07";
		public const string LastChangesText = "Arreglados problemas de importación de Maquinistas";
		public const string VersionColor = "#20A0A0"; //Color de la versión para diferenciar una de otra.

		/// <summary>
		/// Estados posibles en los que puede encontrarse un tren
		/// </summary>		
		public enum TrainStatus : byte
		{
			Unknown = 0,             //Estado desconocido
			Available = 1,            //Disponible en manos de la operadora
			DepotRequested = 2,       //Disponible, solicitado por taller para mantenimiento
			DepotAvailable = 3,       //Disponible pero reservado para mantenimiento en talleres
			RequestToDiagnose = 4,    //Se ha abierto un parte de averías, pendiente de diagnóstico
			RequestToRepair = 5,      //Diagnosticado. Pendiente de retirar del servicio
			Repairing = 6,            //En taller para reparaciones
			Maintenance = 7,          //En taller por mantenimiento programado o campañas
			StandStill = 8,           //De baja, pero sigue pasando revisiones
			Disabled = 9,             //De baja sin mantenimiento
			NoneSelected=255	      //Sin estado seleccionado (para el modelo)
		}

		public static string TrainStatusToString(TrainStatus status)
		{
			if ((byte)status < TrainStatusString.Length)
				return TrainStatusString[(byte)status];
			return "Desconocido";
		}

		public static readonly string[] TrainStatusString =
		{
			"Desconocido",
			"Disponible",
			"Solicitado Preventivo",
			"Apartado Preventivo",
			"A Diagnósticar",
			"Para retirar",
			"Correctivo",
			"Preventivo",
			"StandStill",
			"De baja"
		};
		public static readonly string[] TrainStatusTelegramString =
		{
			"está en estado desconocido. No tengo datos sobre él.",
			"está disponible para la circulación.",
			"tiene una solicitud pendiente para revisión.",
			"debe ser retirado de la circulación para entrar a reparar.",
			"está en talleres, en proceso de reparación.",
			"está en talleres, en una revisión.",
			"se encuentra en situación de parada prolongada (stand-still).",
			"ya no se encuentra en el sistema. Está de baja."
		};

		public enum TrainViewType : byte //Pantalla de lista de trenes que vamos a mostrar
		{
			Unknown = 0,
			Activation = 1, //Activación de trenes (muestra la lista de trenes recién creados)
			RepairPendant = 2, //Trenes que esperan entrar en el taller por correctivo.
			Repairing = 3, //Trenes en el taller.
			Available = 4, //Trenes disponibles.
		}


		/// <summary>
		/// Transiciones de estado permitidas
		/// </summary>
		public enum OperationType : byte
		{
			Activate = 0,             //Activación de un tren
									  // Unknown, StandStill o Disabled a RequestToRepair.
									  // Administrador
			CorrectiveRequest = 1,    //Se abre una incidencia sobre un tren activo.
									  // Available, DepotRequested o DepotAvailable a RequestToDiagnose
									  // [Todos los usuarios]
			DiagnoseToFault = 2,      //Diagnóstico para retirar tren
									  // RequestToDiagnose a RequestToRepair
									  // Jefe de Maquinistas, Taller
			DiagnoseToAvailable = 3,  //Diagnóstico que permite retornar el tren a la circulación
									  // RequestToDiagnose a Available
									  // Jefe de Maquinistas, Taller
			BeginCorrective = 4,      //Recepción de una unidad para reparar en taller
									  // RequestToRepair o Maintenance a Repairing
									  // Taller
			EndCorrective = 5,        //Finalización de la reparación
									  // Repairing a Available
									  // Taller
			DepotRequest = 6,         //Solicitud para mantenimiento
									  // Available a DepotRequested
									  // Taller, [Planificador]
			DepotRequestAccept = 7,   //Aceptación de apartado de tren para mantenimiento planificado
									  // DepotRequested a DepotAvailable
									  // Inspector de Operaciones
			DepotRequestDeny = 8,     //Denegación de apartado de tren para mantenimiento planificado
									  // DepotRequested a Available
									  // Inspector de Operaciones
			MaintenanceRescue = 9,   //Solicitud de reincorporación de un tren apartado para mantenimiento
									 // DepotAvailable a DepotRequested
									 // Inspector de Operaciones
			BeginMaintenance = 10,   //Entrada para mantenimiento
									 // DepotAvailable a Maintenance
									 // Taller
			EndMaintenance = 11,     //Fin de mantenimiento
									 // Maintenance a Available
									 // Taller
			DiferMaintenance = 12,   //Fin parcial de mantenimiento.
									 // Maintenance a DepotAvailable
									 // Taller
			SendToStandStill = 13,   //Baja de la circulación
									 // Cualquier estado a StandStill
									 // Administrador
			RescueFromStandStill = 14, //Rescate desde StandStill
									   // StandStill a RequestToRepair
									   // Administrador
			SendToDisabled = 15,      //Baja definitiva
									  // StandStill a Disabled
									  // Administrador
			RescueFromDisabled = 16,  //Rescate de baja definitiva
									  // Disabled a StandStill
									  // Administrador
			Unknown = 255 //Operación anómala.
		}

		public enum CacheTableKey : byte
		{
			None = 0, //Ninguna tabla en concreto
			Users = 1, //Tabla de usuarios
			TrainStatus = 2 //Tabla de cambios de estado de los trenes
		}
		//public static OperationType stringToOperation(string? rhs)
		//{
		//	if (null == rhs) return OperationType.Unknown;
		//	string auxNormalized = rhs.ToUpper();
		//	if (auxNormalized.Contains("ACTIVATE")) return OperationType.Activate;
		//	if (auxNormalized.Contains("CORRECTIVEREQUEST")) return OperationType.CorrectiveRequest;
		//	if (auxNormalized.Contains("DIAGNOSETOFAULT")) return OperationType.DiagnoseToFault;
		//	if (auxNormalized.Contains("DIAGNOSETOAVAILABLE")) return OperationType.DiagnoseToAvailable;
		//	if (auxNormalized.Contains("BEGINCORRECTIVE")) return OperationType.BeginCorrective;
		//	if (auxNormalized.Contains("ENDCORRECTIVE")) return OperationType.EndCorrective;
		//	if (auxNormalized.Contains("DEPOTREQUESTACCEPT")) return OperationType.DepotRequestAccept;
		//	if (auxNormalized.Contains("DEPOTREQUESTDENY")) return OperationType.DepotRequestDeny;
		//	if (auxNormalized.Contains("DEPOTREQUEST")) return OperationType.DepotRequest;
		//	if (auxNormalized.Contains("MAINTENANCERESCUE")) return OperationType.MaintenanceRescue;
		//	if (auxNormalized.Contains("BEGINMAINTENANCE")) return OperationType.BeginMaintenance;
		//	if (auxNormalized.Contains("ENDMAINTENANCE")) return OperationType.EndMaintenance;
		//	if (auxNormalized.Contains("DIFERMAINTENANCE")) return OperationType.DiferMaintenance;
		//	if (auxNormalized.Contains("SENDTOSTANDSTILL")) return OperationType.SendToStandStill;
		//	if (auxNormalized.Contains("RESCUEFROMSTANDSTILL")) return OperationType.RescueFromStandStill;
		//	if (auxNormalized.Contains("SENDTODISABLED")) return OperationType.SendToDisabled;
		//	if (auxNormalized.Contains("RESCUEFROMDISABLED")) return OperationType.RescueFromDisabled;
		//	return OperationType.Unknown;
		//}

		/// <summary>
		/// Estados de una orden de trabajo.
		/// </summary>
		public enum ActionRecordStatus : byte
		{
			Issued = 0,       //Emitido
			Assigned = 1,     //Asignado
			Active = 2,       //Activo
			Terminated = 3    //Terminado
		}

		/// <summary>
		/// Flags de los tipos de operación que se pueden realizar sobre un elemento del tren
		/// </summary>
		public enum RecordOperationType : byte
		{
			None = 0,        //Ninguna operación
			Install = 1,      //Instalación desde pieza parque
			Remove = 2,       //Retirada de esta pieza al parque o deshecho
			Repair = 4,       //Reparación de pieza averiada
			Clean = 8,        //Limpieza de pieza averiada
			Inspection = 16,  //Comprobación o inspección de la pieza
			Service = 32,     //Sustitución de algún fungible (cambio de filtro, aceite, escobillas, etc.)
			Other = 64,       //Otro tipo de operación no listada
			Unknown = 128     //Operación desconocida
		}

		/// <summary>
		/// Enumeración de los diferentes roles que pueden existir en el programa
		/// </summary>
		public enum UserRole : byte
		{
			Anonymous = 0,    //Invitados y usuarios generales
			Inspector = 1,    //Inspectores de circulación
			Expert = 2,       //Usuario que puede emitir diagnósticos
			Oficial = 3,      //Oficial de taller (de SFM)
			Mechanic = 4,     //Operario del taller (contrata)
			Root = 5,         //Usuario administrador con máximos privilegios
			Engineer = 6,     //Ingeniero que accede a la base de datos para consultar informes.
		}

		public enum sessionEventType : byte
		{
			undefined = 0,      //Evento sin describir
			login = 1,          //El usuario inició sesión
			logout = 2,         //El usuario cerró sesión
			sessionExpiry = 3,  //La sesión abierta de un usuario expiró
			badPassword = 4,        //Error de credenciales
			banned = 5          //Usuario expulsado por un administrador
		}

		//public static UserRole fromRoleName(string roleName)
		//{
		//	switch(roleName.ToUpper())
		//	{
		//		case "ENGINEER": return UserRole.Engineer;
		//		case "MECHANIC": return UserRole.Mechanic;
		//		case "ROOT": return UserRole.Root;
		//		case "INSPECTOR": return UserRole.Inspector;
		//		case "OFICIAL": return UserRole.Oficial;
		//		case "EXPERT": return UserRole.Expert;			
		//	}
		//	return UserRole.Anonymous;
		//}

		static public string timeStringTelegram(DateTime? rhs)
		{
			if (null == rhs) return "-";
			DateTime secc = ((DateTime)rhs).AddHours(2);
			string cadenaFormato = "{0:dd-MM-yy} ({0:HH:mm})";
			if (DateTime.Now.Subtract(secc).Ticks > 0) //Tiempo pasado
			{
				if (DateTime.Now.Subtract(secc).Days < 1)
				{
					cadenaFormato = "hoy, a las {0:HH:mm}";
				}
				else if (DateTime.Now.Subtract(secc).Days < 2)
				{
					cadenaFormato = "ayer, a las {0:HH:mm}";
				}
				else if (DateTime.Now.Subtract(secc).Days > 30)
				{
					cadenaFormato = "el {0:dd} de {0:MM} a las {0:hh:mm}";
				}
			}
			else //Tiempo futuro
			{
				cadenaFormato = "el {0:dd} de {0:MM} a las {0:hh:mm}";
			}			
			return string.Format(cadenaFormato, secc);
		}
	
		static public string phoneString(string? rhs)
		{
			if (string.IsNullOrEmpty(rhs))
				return string.Empty;

			//Eliminamos caracteres que no sean dígitos
			string soloDigitos = new string(rhs.Where(char.IsDigit).ToArray());
			if (9 == soloDigitos.Length)
				return $"{soloDigitos.Substring(0, 3)} {soloDigitos.Substring(3, 3)} {soloDigitos.Substring(6, 3)}";

			return rhs; //Si no tiene nueve dígitos, devolvemos lo que teníamos.
		}

		static public DateTime? parseSapphireDate(string? rhs)
		{
			if (null == rhs) return null;
			DateTime salida;
			if (DateTime.TryParseExact(
				rhs,
				new[] { "d-M-yyyy", "dd-MM-yyyy", "d-M-yy", "dd-MM-yy" },
				System.Globalization.CultureInfo.InvariantCulture,
				System.Globalization.DateTimeStyles.None,
				out salida))
				return salida;

			return null;
		}
		static public TimeSpan? parseSapphireTimeSpan(string? rhs)
		{
			if (null == rhs) return null;
			TimeSpan salida;
			if (TimeSpan.TryParseExact(rhs, new[] { "hh\\:mm", "h\\:mm" }, System.Globalization.CultureInfo.InvariantCulture, out salida))
				return salida;
			return null;
		}
	
	}
}
