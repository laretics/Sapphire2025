using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2025Models
{
	public static class Common
	{
		public static string SapphireSoftwareVersion => "26.8.10";
		public static bool MajorVersion => true; //Muestra el aviso de actualización sólo cuando este flag es true.
		public static string LastChangesText => "Libros Itinerario";

		public static readonly Guid TelegramToken = new Guid("3a7f9c2e-8b4d-4f1a-9e6c-7d2b5a8f3e1c"); //Token de sesión sólo para operaciones desde Telegram.

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
			"Pendiente decisión parada",
			"A retirar",
			"Correctivo",
			"Preventivo",
			"StandStill",
			"De baja"
		};
		public static readonly string[] TrainStatusColor =
		{
			"#C7877B",	//Desconocido
			"#A2F090",	//Disponible
			"#90F0DD",	//Solicitado preventivo
			"#6BB5A8",  //Apartado preventivo
			"#D3D660",	//Pendiente decisión parada
			"#D69160",	//A retirar
			"#D95F5F",	//Correctivo
			"#1CA38C",	//Preventivo
			"#776EFF",	//Standstill
			"#C2C2C2",	//De baja
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

		public static string GetVersionString
		{
			  get 
			  {
				StringBuilder salida = new StringBuilder();
				salida.AppendLine("Sapphire 2026 HTTP Server");
				salida.AppendLine("=========================");
				salida.AppendFormat("Version {0}\n", SapphireSoftwareVersion);
				salida.AppendLine("Last changes:");
				string[] lineas = LastChangesText.Split('|');
				foreach (string line in lineas)
					salida.AppendLine(line);
				salida.AppendFormat("Current local time is {0:HH:mm}", DateTime.Now);
				return salida.ToString();
			  }
		}

		public static Guid WorkOrderTypeManualWash = new Guid("2f8c7d4a-8e1b-4c3a-9d62-1b7e5a0f4c91");
		public static Guid WorkOrderTypePlatformWash = new Guid("c6a1b3d9-3f5e-4e7a-8d21-0f9c6b4a2d78");
		public static Guid WorkOrderTypeTunnelWash = new Guid("91e4c0b7-6a2d-4f18-b3c9-7d0e5a2f8b14");


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
			Station = 7,	  //Gestor de estaciones
		}

		public enum sessionEventType : byte
		{
			undefined = 0,      //Evento sin describir
			login = 1,          //El usuario inició sesión
			logout = 2,         //El usuario cerró sesión
			sessionExpiry = 3,  //La sesión abierta de un usuario expiró
			badPassword = 4,    //Error de credenciales
			banned = 5,         //Usuario expulsado por un administrador

			// Administración de usuarios
			userCreated = 10,           //Se creó un nuevo usuario
			userModified = 11,          //Se modificaron datos de un usuario
			userRolesChanged = 12,      //Se cambiaron roles de un usuario
			passwordReset = 13,         //Un admin reseteó la contraseña
			passwordSet = 14,           //El usuario estableció su contraseña
			telegramPaired = 15,        //Usuario emparejado con Telegram
			telegramUnpaired = 16,      //Usuario desemparejado de Telegram
			telegramPairingRequested = 17, //Se solicitó código de emparejado Telegram

			// Operaciones sobre trenes (Aeneas)
			trainStatusChanged = 20,    //Cambio de estado de un tren
			trainPlatformChanged = 21,  //Cambio de vía/andén de un tren
			trainWashUpdated = 22,      //Actualización de fecha de lavado
			trainOdometerUpdated = 23,  //Actualización de odómetro de un tren

			// Notas e incidencias
			noteAdded = 30,             //Se abrió/añadió una nota
			incidentOpened = 31,        //Se abrió un parte de incidencia (correctivo)

			// Órdenes de trabajo / lavados (GMao)
			workOrderRequested = 40,    //Se solicitó una orden de trabajo (p.ej. lavado)
			workOrderOpened = 41,       //Se inició una orden de trabajo
			workOrderClosed = 42,       //Se finalizó una orden de trabajo
			workOrderVerified = 43,     //Se verificó una orden de trabajo
			workOrderRejected = 44,     //Se rechazó una orden de trabajo

			// Telegram y comunicaciones
			telegramBroadcast = 50,     //Broadcast por Telegram

			// Expert / TimeNet (operaciones de escritura relevantes)
			expertDataImported = 60,    //Importación de datos Expert
			expertPlanDeleted = 61,     //Borrado de plan de explotación
			festiveChanged = 62,        //Cambio de festivo
			timeNetUploaded = 63,       //Subida de topología/rautatie TimeNet
			timeNetTopoDeleted = 64,    //Borrado de TopoStorage

			// Cuadrante de maquinistas (SchedulesManagement)
			scheduleViewOpened = 70,    //Apertura de la vista de cuadrante
			scheduleDayViewed = 71,     //Consulta de un día del cuadrante propio (o simulado)
			scheduleShiftQueried = 72,  //Consulta de quién realiza un turno
			scheduleSwapRequested = 73, //Solicitud de cambio de turno

			// Documentación de circulación (Diamond / libro itinerario)
			circulationBookPrinted = 80,       //Impresión de libro itinerario
			circulationSheetPrinted = 81,      //Impresión de ficha de un tren
			circulationBookExportedPdf = 82,   //Exportación PDF de libro
			circulationSheetExportedPdf = 83,  //Exportación PDF de ficha
			circulationSealVerified = 84       //Verificación de sello SEL en UI
		}

		/// <summary>
		/// Nombre legible de un evento de sesión/actividad de usuario.
		/// </summary>
		public static string SessionEventTypeName(sessionEventType evento)
		{
			return evento switch
			{
				sessionEventType.login => "Inicio de sesión",
				sessionEventType.logout => "Cierre de sesión",
				sessionEventType.sessionExpiry => "Sesión expirada",
				sessionEventType.banned => "Usuario expulsado",
				sessionEventType.badPassword => "Error de password",
				sessionEventType.userCreated => "Creación de usuario",
				sessionEventType.userModified => "Modificación de usuario",
				sessionEventType.userRolesChanged => "Cambio de roles",
				sessionEventType.passwordReset => "Reseteo de contraseña",
				sessionEventType.passwordSet => "Establecimiento de contraseña",
				sessionEventType.telegramPaired => "Emparejado Telegram",
				sessionEventType.telegramUnpaired => "Desemparejado Telegram",
				sessionEventType.telegramPairingRequested => "Solicitud emparejado Telegram",
				sessionEventType.trainStatusChanged => "Cambio de estado de tren",
				sessionEventType.trainPlatformChanged => "Cambio de vía de tren",
				sessionEventType.trainWashUpdated => "Actualización de lavado",
				sessionEventType.trainOdometerUpdated => "Actualización de odómetro",
				sessionEventType.noteAdded => "Nota añadida",
				sessionEventType.incidentOpened => "Apertura de incidencia",
				sessionEventType.workOrderRequested => "Solicitud de orden de trabajo",
				sessionEventType.workOrderOpened => "Inicio de orden de trabajo",
				sessionEventType.workOrderClosed => "Finalización de orden de trabajo",
				sessionEventType.workOrderVerified => "Verificación de orden de trabajo",
				sessionEventType.workOrderRejected => "Rechazo de orden de trabajo",
				sessionEventType.telegramBroadcast => "Broadcast Telegram",
				sessionEventType.expertDataImported => "Importación Expert",
				sessionEventType.expertPlanDeleted => "Borrado de plan Expert",
				sessionEventType.festiveChanged => "Cambio de festivo",
				sessionEventType.timeNetUploaded => "Subida TimeNet",
				sessionEventType.timeNetTopoDeleted => "Borrado topología TimeNet",
				sessionEventType.scheduleViewOpened => "Apertura de cuadrante",
				sessionEventType.scheduleDayViewed => "Consulta día de cuadrante",
				sessionEventType.scheduleShiftQueried => "Consulta de turno ajeno",
				sessionEventType.scheduleSwapRequested => "Solicitud de cambio de turno",
				sessionEventType.circulationBookPrinted => "Impresión libro itinerario",
				sessionEventType.circulationSheetPrinted => "Impresión ficha de circulación",
				sessionEventType.circulationBookExportedPdf => "PDF libro itinerario",
				sessionEventType.circulationSheetExportedPdf => "PDF ficha de circulación",
				sessionEventType.circulationSealVerified => "Verificación sello de circulación",
				_ => "¿?"
			};
		}

		public enum TrainSystem: byte
		{
			undefined=0,		//Sistema indeterminado
			CCTV=1,				//Vigilancia CCTV y grabación
			ExtSignaling=2,		//Señalización exterior (luces y avisos)
			Doors=3,			//Puertas exteriores
			CabDoors=4,			//Puertas de la cabina
			HVAC=5,				//Sistema de climatización
			Traction=6,			//Sistema eléctrico de tracción (Inversores, motores y bobinas de filtro)
			Braking=7,			//Sistema de freno (Neumático)
			Converters=8,		//Convertidores auxiliares
			CockPit=9,			//Equipamiento de cabina de conducción
			SIV=10,				//Megafonía e información al viajero
			Vandalism=11,		//Grafitis y desperfectos por vandalismo
			Underhood=12,		//Rodamiento, suspensión y chasis inferior
			HighVoltage=13,		//Elementos de alta tensión
			FAP=14,				//FAP, ASFA, ERTMS y similares
			Pneumatics=15,		//Sistema neumático TDP, compresores y relacionados
			Exterior=16,		//Carrocería y remates
		}

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
