using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Sapphire2026.Data.Models.Turnos
{
    /// <summary>
    /// Contenido de un turno de trabajo.
    /// </summary>
    [Table("WorkShiftContent")]
    public class WorkShiftContent
    {
        [Key]
        public Guid Id { get; set; } // Identificador único del contenido del turno
        public Guid Parent { get; set; } //Identificador del template de turno
        public Guid ParentCollection { get; set; } //Identificador de la colección de turnos a la que pertenece este contenido (Para facilitar el borrado)
        public byte ContentType { get; set; } //0: depósito, 1: tren
        public string? TrainId { get; set; } //Identificador del tren (sólo para turnos de conducción)
        public bool Discrectional { get; set; } //Trabajo discreccional (si es true, no es obligatorio que sea realizado)
        public bool Foreign { get; set; } //Indica si el depósito o ATT se realiza fuera de la base (turnos de Inca o Manacor)

        public TimeSpan Begin { get; set; } //Hora de inicio del tren o depósito
        public TimeSpan Duration { get; set; } //Duración del tren o depósito
        [NotMapped] 
        public TimeSpan EndTime => Begin.Add(Duration); //Hora de fin del tren o depósito, calculada como la suma de la hora de inicio y la duración
    }
}
