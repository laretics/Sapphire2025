using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2025Models.Expert
{
	public class AssignationContentModel
	{
		public Guid AgentId { get; set; } //Id del Agente
		public DateTime Date { get; set; } //Fecha de la asignación.
		public string? AssignationsChain { get; set; } //Cadena de asignaciones.
		public string? Definitive { get; set; } //Asignación definitiva.
		public string? Comment { get; set; } //Notas del Jefe de Maquinistas.
		public bool TD { get; set; } //Es un turno en descanso. Habrá que ponerlo en verde.
		public Guid SwappingAgent { get; set; } //Agente al que hace el turno (si lo hay)
	}
}
