using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
namespace Sapphire2026.Data.Models.Turnos
{
    /// <summary>
    /// Lista de Agentes para mostrar en la vista de gráfico de turnos.
    /// Las listas pueden contener agentes individuales, separadores o
    /// también otras listas de agentes.
    /// </summary>
    [Table("ExpertAgentsListView")]
    public class ExpertAgentsListView
    {
        [Key]
        public Guid Id { get; set; } // Id de la lista de agentes
        public string Name { get; set; } // Nombre de la lista de agentes
        public string? Comments { get; set; } // Descripción de la lista de agentes
        public bool Final { get; set; } // Indica si esta lista se muestra en el menú.
    }
}
