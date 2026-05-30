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
        internal TrainOrderConcept(IConfiguration config,IServiceProvider provider, Sapphire2025Models.Common.OperationType operation):base(config,provider) 
        {
            this.Operation = operation;
        }


    }
}
