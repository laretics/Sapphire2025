using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Sapphire2025Server.Models.Turnos
{
    /// <summary>
    /// Asignación de un turno de trabajo a un Maquinista
    /// </summary>

    [Table("WorkShiftAssignation")]
    public class WorkshiftAssignation
    {
        public Guid Id { get; set; }
        public string? CF { get; set; } //Carnet ferroviario del Agente
        public DateTime Date {get;set;} //Fecha de la asignación
        public string? Assignation { get; set; } //Cadena de asignaciones
        public string? Definitive { get; set; } //Asignación definitiva (precalculada)
        public string? SwappingCF { get; set; } //CF del Agente con el que se cambia la asignación
    }
}
