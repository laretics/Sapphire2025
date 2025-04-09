namespace ZafiroGmao.Data.Models
{
    public static class Common
    {
        /// <summary>
        /// Estados posibles en los que puede encontrarse un tren
        /// </summary>
        public enum TrainStatus:byte
        {
            Unknown =0,             //Estado desconocido
            Available=1,            //Disponible en manos de la operadora
            DepotRequested=2,       //Disponible, solicitado por taller para mantenimiento
            DepotAvailable=3,       //Disponible pero reservado para mantenimiento en talleres
            RequestToDiagnose=4,    //Se ha abierto un parte de averías, pendiente de diagnóstico
            RequestToRepair=5,      //Diagnosticado. Pendiente de retirar del servicio
            Repairing=6,            //En taller para reparaciones
            Maintenance=7,          //En taller por mantenimiento programado o campañas
            StandStill=8,           //De baja, pero sigue pasando revisiones
            Disabled=9,             //De baja sin mantenimiento
        }

        public static readonly string[] TrainStatusString =
        {
            "Estado Desconocido",
            "Disponible",
            "Disponible (Solicitado Mantenimiento)",
            "Apartado para Mantenimiento",
            "Pendiente de Diagnóstico",
            "Necesario retirar",
            "En reparación",
            "En mantenimiento",
            "StandStill",
            "De baja"
        };

        public enum TrainViewType:byte //Pantalla de lista de trenes que vamos a mostrar
        {
            Unknown =0,
            Activation = 1, //Activación de trenes (muestra la lista de trenes recién creados)
            RepairPendant = 2, //Trenes que esperan entrar en el taller por correctivo.
            Repairing=3, //Trenes en el taller.
            Available=4, //Trenes disponibles.
        }


        /// <summary>
        /// Transiciones de estado permitidas
        /// </summary>
        public enum OperationType:byte
        {
            Activate=0,             //Activación de un tren
            // Unknown, StandStill o Disabled a RequestToRepair.
            // Administrador
            CorrectiveRequest=1,    //Se abre una incidencia sobre un tren activo.
            // Available, DepotRequested o DepotAvailable a RequestToDiagnose
            // [Todos los usuarios]
            DiagnoseToFault=2,      //Diagnóstico para retirar tren
            // RequestToDiagnose a RequestToRepair
            // Jefe de Maquinistas, Taller
            DiagnoseToAvailable=3,  //Diagnóstico que permite retornar el tren a la circulación
            // RequestToDiagnose a Available
            // Jefe de Maquinistas, Taller
            BeginCorrective=4,      //Recepción de una unidad para reparar en taller
            // RequestToRepair o Maintenance a Repairing
            // Taller
            EndCorrective=5,        //Finalización de la reparación
            // Repairing a Available
            // Taller
            DepotRequest=6,         //Solicitud para mantenimiento
            // Available a DepotRequested
            // Taller, [Planificador]
            DepotRequestAccept=7,   //Aceptación de apartado de tren para mantenimiento planificado
            // DepotRequested a DepotAvailable
            // Inspector de Operaciones
            DepotRequestDeny=8,     //Denegación de apartado de tren para mantenimiento planificado
            // DepotRequested a Available
            // Inspector de Operaciones
            MaintenanceRescue=9,   //Solicitud de reincorporación de un tren apartado para mantenimiento
            // DepotAvailable a DepotRequested
            // Inspector de Operaciones
            BeginMaintenance=10,   //Entrada para mantenimiento
            // DepotAvailable a Maintenance
            // Taller
            EndMaintenance=11,     //Fin de mantenimiento
            // Maintenance a Available
            // Taller
            DiferMaintenance=12,   //Fin parcial de mantenimiento.
            // Maintenance a DepotAvailable
            // Taller
            SendToStandStill=13,   //Baja de la circulación
            // Cualquier estado a StandStill
            // Administrador
            RescueFromStandStill=14, //Rescate desde StandStill
            // StandStill a RequestToRepair
            // Administrador
            SendToDisabled=15,      //Baja definitiva
            // StandStill a Disabled
            // Administrador
            RescueFromDisabled=16,  //Rescate de baja definitiva
            // Disabled a StandStill
            // Administrador
            Unknown = 255 //Operación anómala.
        }
        public static OperationType stringToOperation(string? rhs)
        {
            if (null == rhs) return OperationType.Unknown;
            string auxNormalized = rhs.ToUpper();
            if (auxNormalized.Contains("ACTIVATE")) return OperationType.Activate;
            if (auxNormalized.Contains("CORRECTIVEREQUEST")) return OperationType.CorrectiveRequest;
            if (auxNormalized.Contains("DIAGNOSETOFAULT")) return OperationType.DiagnoseToFault;
            if (auxNormalized.Contains("DIAGNOSETOAVAILABLE")) return OperationType.DiagnoseToAvailable;
            if (auxNormalized.Contains("BEGINCORRECTIVE")) return OperationType.BeginCorrective;
            if (auxNormalized.Contains("ENDCORRECTIVE"))return OperationType.EndCorrective;
			if (auxNormalized.Contains("DEPOTREQUESTACCEPT")) return OperationType.DepotRequestAccept;
            if (auxNormalized.Contains("DEPOTREQUESTDENY")) return OperationType.DepotRequestDeny;
			if (auxNormalized.Contains("DEPOTREQUEST")) return OperationType.DepotRequest;
            if (auxNormalized.Contains("MAINTENANCERESCUE")) return OperationType.MaintenanceRescue;
            if (auxNormalized.Contains("BEGINMAINTENANCE")) return OperationType.BeginMaintenance;
            if (auxNormalized.Contains("ENDMAINTENANCE")) return OperationType.EndMaintenance;
            if (auxNormalized.Contains("DIFERMAINTENANCE")) return OperationType.DiferMaintenance;
            if (auxNormalized.Contains("SENDTOSTANDSTILL")) return OperationType.SendToStandStill;
            if (auxNormalized.Contains("RESCUEFROMSTANDSTILL")) return OperationType.RescueFromStandStill;
            if (auxNormalized.Contains("SENDTODISABLED")) return OperationType.SendToDisabled;
            if (auxNormalized.Contains("RESCUEFROMDISABLED")) return OperationType.RescueFromDisabled;
            return OperationType.Unknown;            
        }

        /// <summary>
        /// Estados de un expediente.
        /// </summary>
        public enum ActionRecordStatus:byte
        {
            Issued=0,       //Emitido
            Assigned=1,     //Asignado
            Active=2,       //Activo
            Terminated=3    //Terminado
        }

        /// <summary>
        /// Flags de los tipos de operación que se pueden realizar sobre un elemento del tren
        /// </summary>
        public enum RecordOperationType: byte
        {
            None =0,        //Ninguna operación
            Install=1,      //Instalación desde pieza parque
            Remove=2,       //Retirada de esta pieza al parque o deshecho
            Repair=4,       //Reparación de pieza averiada
            Clean=8,        //Limpieza de pieza averiada
            Inspection=16,  //Comprobación o inspección de la pieza
            Service=32,     //Sustitución de algún fungible (cambio de filtro, aceite, escobillas, etc.)
            Other=64,       //Otro tipo de operación no listada
            Unknown=128     //Operación desconocida
        }

        /// <summary>
        /// Enumeración de los diferentes roles que pueden existir en el programa
        /// </summary>
        public enum UserRole: byte
        {
            Anonymous=0,    //Invitados y usuarios generales
            Inspector=1,    //Inspectores de circulación
            Expert=2,       //Usuario que puede emitir diagnósticos
            Oficial=3,      //Oficial de taller (de SFM)
            Mechanic=4,     //Operario del taller (contrata)
            Root=5,         //Usuario administrador con máximos privilegios
            Engineer=6,     //Ingeniero que accede a la base de datos para consultar informes.
        }
            

    }
}
