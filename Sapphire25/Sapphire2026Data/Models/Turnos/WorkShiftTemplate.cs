using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Sapphire2026.Data.Models.Turnos
{
    /// <summary>
    /// Esto es un turno de trabajo que puede realizar un determinado Agente.
    /// </summary>
    [Table("WorkShiftTemplate")]
    public class WorkShiftTemplate
    {
        [Key]
        public Guid Id { get; set; } //Identificador único del turno de trabajo
        public Guid Parent { get; set; } //Identificador del proyecto de explotación al que pertenece este turno
        [Required]
        public string Name { get; set; } //Nombre del turno de trabajo (se separan por comas todas las cadenas aceptadas)        
        public string Tokens { get; set; } //Lista separada por comas con todas las cadenas que se reconocen como este turno.
        public string? Comment { get; set; } //Comentario o descripción del turno de trabajo
        public TimeSpan StartTime { get; set; } //Hora de inicio del turno de trabajo
        public TimeSpan Duration { get; set; } //Duración del turno de trabajo
        [NotMapped]
        public TimeSpan EndTime => StartTime.Add(Duration); //Hora de fin del turno de trabajo, calculada como la suma de la hora de inicio y la duración
        public bool Att { get; set; } //Es atención a trenes.
        public bool Active { get; set; } //Indica si el turno de trabajo se considera como laboral. Lo contrario es un descanso o licencia.
        public string? Color { get; set; } //Color asociado al turno de trabajo, las letras
        public string? BgColor { get; set; } //Color del fondo de la celda
        public string? StripeColor { get; set; } //Color de la franja (suponiendo que esta celda tenga franja)
        public byte PerWeek { get; set; } //Días de la semana en que aplica este turno.
        public int CoorX { get; set; } //Lugar de representación en la tabla del gráfico
		public int CoorY { get; set; } //Coordenada y de representación en la tabla del gráfico
	}
}
