using System;
using System.Collections.Generic;
using System.Text;

namespace TimeNet2026.Obsidian
{
	public class ScheduleMetrics
	{
		public string DriverId { get; set; } = string.Empty;

		// Horarios del turno
		public TimeSpan ScheduleBegin { get; set; }
		public TimeSpan ScheduleEnd { get; set; }
		public TimeSpan ScheduleTime => ScheduleEnd - ScheduleBegin;

		// Tiempos de conducción y descanso
		public TimeSpan DrivingTime { get; set; }
		public TimeSpan IddleTime =>ScheduleTime>DrivingTime
			?  ScheduleTime-DrivingTime
			: TimeSpan.Zero;

		public int TrainsCount { get; set; }
		public int BlocksCount { get; set; }           // útil si quieres ver bloques de 1 o 2 trenes

		// Eficiencia
		public double Efficiency => ScheduleTime.TotalMinutes > 0
			? (DrivingTime.TotalMinutes / ScheduleTime.TotalMinutes) * 100
			: 0;

		public double  DrivingRatio => IddleTime.TotalMinutes > 0
			? DrivingTime.TotalMinutes / IddleTime.TotalMinutes
			: 0;   // > 1 es muy eficiente

		// Estadísticas de pausas / gaps
		public TimeSpan IddleMin { get; set; } = TimeSpan.MaxValue;
		public TimeSpan IddleMax { get; set; } = TimeSpan.Zero;
		public TimeSpan IddleAverage { get; set; } = TimeSpan.Zero;
		public int GapsCount { get; set; } = 0;   // número de gaps entre servicios

		// Reparto de carga (para ver si la jornada está bien distribuida)
		public List<(TimeSpan Hora, double MinutosConduccion)> AssignationsByHour { get; set; } = new();

		// Máxima conducción continua (ya la controlas, pero es buena métrica)
		public TimeSpan MaxDrivingTime { get; set; } = TimeSpan.Zero;

		public override string ToString()
		{
			var sb = new StringBuilder();
			sb.AppendLine($"   Métricas - Maquinista {DriverId}");
			sb.AppendLine($"   Turno: {ScheduleBegin:hh\\:mm} → {ScheduleEnd:hh\\:mm}  (Jornada: {ScheduleTime:hh\\:mm})");
			sb.AppendLine($"   Conducción total: {DrivingTime:hh\\:mm}   |   Descanso: {IddleTime:hh\\:mm}");
			sb.AppendLine($"   Trenes: {TrainsCount}   |   Bloques: {BlocksCount}");
			sb.AppendLine($"   Eficiencia jornada: {Efficiency:F1}%   |   Ratio cond./desc.: {DrivingRatio:F2}");
			sb.AppendLine($"   Pausas: min={IddleMin:hh\\:mm}  max={IddleMax:hh\\:mm}  avg={IddleAverage:hh\\:mm}  (n={GapsCount})");
			sb.AppendLine($"   Max conducción continua: {MaxDrivingTime:hh\\:mm}");

			if (AssignationsByHour.Any())
			{
				sb.AppendLine("   Distribución de carga:");
				foreach (var (hora, minutos) in AssignationsByHour)
					sb.AppendLine($"     {hora:hh\\:mm} → {minutos:F0} min conducción");
			}
			return sb.ToString();
		}
	}
}
