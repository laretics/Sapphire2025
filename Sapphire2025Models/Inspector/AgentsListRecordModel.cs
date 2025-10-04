using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2025Models.Inspector
{
	public class AgentsListRecordModel
	{
		public bool Even { get; set; } //Indicador de paridad para sombreado en la lista.
		public string? Color { get; set; } //Color del turno
		public string? BgColor { get; set; } //Color de fondo del turno
		public bool Covered { get; set; } //Turno cubierto
		public bool Att { get; set; } //El turno es Atención a trenes.
		public string? ScheduleId { get; set; } //Número del turno
		public string? ScheduleTime { get; set; } //Horario de este turno
		public string? ChangedCF { get; set; } //CF del Maquinista que cambia el turno
		public string? ChangedAgentName { get; set; } //Nombre del Maquinista que cambia el turno
		public string? CF { get; set; } //CF del Maquinista que hace el turno
		public string? AgentName { get; set; } //Nombre del Maquinista que hace el turno
		public string? Phone { get; set; } //Teléfono del Maquinista
		public string? Extension { get; set; } //Extensión de Empresa
	}
}
