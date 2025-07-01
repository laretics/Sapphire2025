using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Sapphire2025Server.Models.Turnos
{
    /// <summary>
    /// Proyecto de explotación o libro itinerario al que se ciñe la prestación de una serie de turnos de trabajo.
    /// Contiene toda la información necesaria para la asignación de turnos a los Agentes.
    /// Los proyectos tienen una fecha de inicio de vigencia y, dependiendo de la planificación, también una de expiración.
    /// Si no se especifica una fecha de expiración o es posterior a la fecha actual, el proyecto está vigente.
    /// </summary>
    [Table("WorkShiftTemplateCollection")]
    public class WorkShiftTemplateCollection
    {
        [Key]
        public Guid Id { get; set; } //Identificador único de la colección
        public string? Name { get; set; } //Nombre de la colección
        public DateTime Begin { get; set; } //Fecha y hora de inicio de la colección
        public DateTime? EndDate { get; set; } //Fecha y hora de fin de la colección
        public string? Comment { get; set; } //Comentario o descripción de la colección
        public byte Collective { get; set; } //Tipo de colectivo al que se aplica la colección (0: Maquinistas, 1: Ayudantes, 2: Otros)
        public Guid? OwnerId { get; set; } //Identificador del propietario de la colección (opcional)
    }
}
