using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace Sapphire2026.Data.Models.Turnos
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
        public string? Annotation { get; set; } //La necesito para gestionar los cambios a tres o más bandas
        [NotMapped]
        public string[] Assignations 
        { 
            get
            {
                if(null == Assignation || Assignation.Length<1)
					return Array.Empty<string>();
                return Assignation.Split('/');
			}                
            set
            {
                this.Assignation = string.Join('/',value);
			}
        }
        [NotMapped]
        public string? LastAssignation
        {
            get
            {
                string[] assigns = Assignations;
                if(assigns.Length>0)
                {
					for (int i = assigns.Length - 1; i >= 0; i--)
					{
						if (assigns[i].Trim().Length > 0)
						{
							string auxAsigna = assigns[i].ToUpper();
							if (!auxAsigna.Contains("RJ") && !auxAsigna.Contains("SJ"))
								return auxAsigna;
						}
					}
				}
                return null;
			}
        }
    }
}
