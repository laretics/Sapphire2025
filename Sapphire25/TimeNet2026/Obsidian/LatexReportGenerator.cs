using System;
using System.Collections.Generic;
using System.Text;
using TimeNet2026.Production;
using TimeNet2026.Timed;

namespace TimeNet2026.Obsidian
{
	internal static class LatexReportGenerator
	{
		public static string GenerateLatexReport(Plan plan, PlanResult resultado, PlanRestrictions specs)
		{
			StringBuilder sb = new StringBuilder();			
			sb.AppendLine(@"\documentclass[12pt,a4paper]{article}");
			sb.AppendLine(@"\usepackage[spanish]{babel}");
			sb.AppendLine(@"\usepackage{geometry}");
			sb.AppendLine(@"\geometry{margin=2.5cm}");
			sb.AppendLine(@"\usepackage{graphicx}");
			sb.AppendLine(@"\usepackage{booktabs}");
			sb.AppendLine(@"\usepackage{subcaption}");
			sb.AppendLine(@"\usepackage{float}");
			sb.AppendLine(@"\usepackage{amsmath}");
			sb.AppendLine(@"\usepackage{caption}");

			sb.AppendLine(@"\usepackage{tikz}");
			sb.AppendLine(@"\usepackage{pgfplots}");
			sb.AppendLine(@"\pgfplotsset{compat=1.18}");

			sb.AppendLine(@"\begin{document}");

			sb.AppendLine(@"\section*{Informe de Planificación de Turnos de Maquinistas}");

			// Lista de trenes
			sb.AppendLine(@"\subsection*{Lista de Trenes}");
			sb.AppendLine(@"\begin{itemize}");
			//var allTrains = project.Blocks.SelectMany(b => b.Trains).Distinct().OrderBy(t => t.HoraSalida).ToList();
			List<Circulation> allTrains = plan.AllCirculationBlocks.SelectMany(b => b.Circulations).Distinct().OrderBy(t => t.departure).ToList();		

			foreach (var tren in allTrains)
			{
				sb.AppendLine($@"  \item \textbf{{ID:}} {tren.name} \hspace{{1cm}} \textbf{{Salida:}} {tren.departure:hh\:mm} \hspace{{1cm}} \textbf{{Llegada:}} {tren.arrival:hh\:mm}");
			}
			sb.AppendLine(@"\end{itemize}");

			// Turnos de Maquinistas y métricas
			sb.AppendLine(@"\subsection*{Turnos de Maquinistas}");
			sb.AppendLine(@"\begin{itemize}");
			var metricsList = new List<ScheduleMetrics>();
			foreach (var maq in resultado.Schedules)
			{
				var metrics = maq.GetMetrics(specs);
				metricsList.Add(metrics);
				sb.AppendLine($@"  \item \textbf{{Maquinista {maq.Id}}}");
				sb.AppendLine(@"  \begin{itemize}");
				sb.AppendLine($@"    \item Trenes asignados: {string.Join(", ", maq.TrenesAsignados.Select(t => t.name))}");
				sb.AppendLine($@"    \item Horario: {metrics.ScheduleBegin:hh\:mm} -- {metrics.ScheduleEnd:hh\:mm}");
				sb.AppendLine($@"    \item Tiempo de conducción: {metrics.DrivingTime:hh\:mm}");
				sb.AppendLine($@"    \item Descanso total: {metrics.IddleTime:hh\:mm}");
				sb.AppendLine($@"    \item Eficiencia: {metrics.Efficiency:F1}\%");
				sb.AppendLine($@"    \item Ratio conducción/descanso: {metrics.DrivingRatio:F2}");
				sb.AppendLine($@"    \item Pausas: min={metrics.IddleMin:hh\:mm}, max={metrics.IddleMax:hh\:mm}, avg={metrics.IddleAverage:hh\:mm} (n={metrics.GapsCount})");
				sb.AppendLine($@"    \item Máxima conducción continua: {metrics.MaxDrivingTime:hh\:mm}");
				sb.AppendLine(@"  \end{itemize}");
			}
			sb.AppendLine(@"\end{itemize}");

			// Estadísticas globales
			sb.AppendLine(@"\subsection*{Estadísticas Globales}");
			sb.AppendLine(@"\begin{itemize}");
			sb.AppendLine($@"  \item Número total de trenes: {allTrains.Count}");
			sb.AppendLine($@"  \item Número total de maquinistas: {resultado.Schedules.Count}");
			sb.AppendLine($@"  \item Media de eficiencia: {metricsList.Average(m => m.Efficiency):F1}\%");
			sb.AppendLine($@"  \item Media de trenes por turno: {metricsList.Average(m => m.TrainsCount):F2}");
			sb.AppendLine($@"  \item Media de pausas por turno: {metricsList.Average(m => m.GapsCount):F2}");
			sb.AppendLine($@"  \item Máxima conducción continua global: {metricsList.Max(m => m.MaxDrivingTime):hh\:mm}");
			sb.AppendLine(@"\end{itemize}");

			// Distribución de carga por hora (tabla)
			sb.AppendLine(@"\subsection*{Distribución de Carga por Hora}");
			var cargaPorHora = allTrains
				.GroupBy(t => t.departure.Hours)
				.OrderBy(g => g.Key)
				.Select(g => new { Hora = g.Key, Minutos = g.Sum(t => (null!=t.Parent.asimilation && t.Parent.asimilation.duration.HasValue)?t.Parent.asimilation.duration.Value.TotalMinutes:0) })
				.ToList();

			sb.AppendLine(@"\begin{center}");
			sb.AppendLine(@"\begin{tabular}{c|c}");
			sb.AppendLine(@"Hora & Minutos de conducción \\");
			sb.AppendLine(@"\hline");
			foreach (var h in cargaPorHora)
			{
				sb.AppendLine($"{h.Hora:00}:00 & {h.Minutos:F0} \\\\");
			}
			sb.AppendLine(@"\end{tabular}");
			sb.AppendLine(@"\end{center}");

			// Ejemplo de gráfica con pgfplots (el usuario debe copiar los datos)
			sb.AppendLine(@"
% Ejemplo de gráfica con pgfplots
\begin{figure}[h!]
\centering
\begin{tikzpicture}
\begin{axis}[
    width=0.8\textwidth,
    xlabel={Hora},
    ylabel={Minutos de conducción},
    xtick=data,
    ybar,
    bar width=15pt,
    nodes near coords,
    symbolic x coords={"
				+ string.Join(",", cargaPorHora.Select(h => $"{h.Hora:00}:00")) +
			@"}
]
\addplot coordinates {"
				+ string.Join(" ", cargaPorHora.Select(h => $"({h.Hora:00}:00,{h.Minutos:F0})")) +
			@"};
\end{axis}
\end{tikzpicture}
\caption{Distribución de la carga de conducción por hora}
\end{figure}
");
			sb.AppendLine(@"\end{document}");
			return sb.ToString();
		}
	}
}
