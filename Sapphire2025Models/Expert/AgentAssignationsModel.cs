using Sapphire2025Server.Expert;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2025Models.Expert
{
	/// <summary>
	/// Clase especial para gestionar las asignaciones de un mismo Agente en función de las fechas.
	/// Tiene la forma de una fila de un array.
	/// </summary>
	public class AgentAssignationsModel
	{
		public ExpertAgentListFormatModel AgentRecord { get; set; } = new ExpertAgentListSeparator();
		public AssignationContent?[]? ColAssignations { get; set; } //Fila de asignaciones

		/// <summary>
		/// Contenido de una asignación a un Agente
		/// </summary>
		public class AssignationContent
		{
			public string? AssignationsChain { get; set; } //Cadena de asignaciones.
			public string? Definitive { get; set; } //Asignación definitiva.
			public string? Comment { get; set; } //Notas del Jefe de Maquinistas.
			public bool TD { get; set; } //Es un turno en descanso. Habrá que ponerlo en verde.
			public Guid SwappingAgent { get; set; } //Agente al que hace el turno (si lo hay)
		}
	}
}
