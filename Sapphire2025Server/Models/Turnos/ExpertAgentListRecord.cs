using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
namespace Sapphire2025Server.Models.Turnos
{
    [Table("ExpertAgentListRecord")]
    public class ExpertAgentListRecord
    {
        [Key]
        public Guid Id { get; set; }
        public Guid ElementId { get; set; } // Id del elemento de la lista (Agente, Separador o Lista)
        public byte Type { get; set; } // Tipo de elemento: 0=Agente, 1=Separador, 2=Lista
        public int Order { get; set; } // Orden del elemento en la lista

    }
}
