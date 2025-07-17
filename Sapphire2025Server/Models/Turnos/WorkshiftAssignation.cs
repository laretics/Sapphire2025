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
        [Key]
        public Guid Id { get; set; }
        public Guid Agent { get; set; } //Guid del Agente que hace el turno
        public Guid SwappingAgent { get; set; } //Id del Agente que ha cambiado (empty si no hay cambio)
        public DateTime Date {get;set;} //Fecha de la asignación
        public string? Assignation { get; set; } //Cadena de asignaciones
        public string? Definitive { get; set; } //Asignación definitiva (precalculada)
        public bool IsTD { get; set; } //Un TD.
        [NotMapped]                                      
        public string? BgColor { get; set; } //La necesito para resolver los cambios entre Agentes
        [NotMapped]
        public string? Annotation { get; set; } //La necesito para gestionar los cambios a tres o más bandas
    }
}
