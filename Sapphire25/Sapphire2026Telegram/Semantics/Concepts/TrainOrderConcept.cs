using Sapphire2025Server.Telegram.Semantics.Concepts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2026Telegram.Semantics.Concepts
{
    /// <summary>
    /// Este tema es una orden recibida sobre un tren. Normalmente provocará un cambio de estado.
    /// </summary>
    internal class TrainOrderConcept:TrainConcept
    {
        internal Sapphire2025Models.Common.OperationType Operation { get; private set; } //Esta es la orden que esperamos leer.
        internal TrainOrderConcept(IConfiguration config, Sapphire2025Models.Common.OperationType operation):base(config) 
        {
            this.Operation = operation;
            switch (operation)
            {
                case Sapphire2025Models.Common.OperationType.BeginCorrective:
                case Sapphire2025Models.Common.OperationType.BeginMaintenance:
				case Sapphire2025Models.Common.OperationType.DiagnoseToFault:
					AddTokens(new string[] { "meter", "taller", "reparar", "intervenir", "actuar", "retirar", "comenzar" });
                    break;
                case Sapphire2025Models.Common.OperationType.EndCorrective:
                case Sapphire2025Models.Common.OperationType.EndMaintenance:
					AddTokens(new string[] { "sacar", "taller", "devolver", "terminar", "actuacion", "reparacion", "mantenimiento","acabar","cerrar" });
                    break;               
                case Sapphire2025Models.Common.OperationType.DiagnoseToAvailable:
					AddTokens(new string[] { "rechazar", "ignorar", "devolver", "seguir", "recuperar", "mantener", "continuar" });
					break;
			}
        }


    }
}
