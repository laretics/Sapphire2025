using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using TimeNet2026.Timed;


namespace TimeNet2026.Obsidian
{
	public class WorkBlock
	{
		public List<Circulation> Trains { get; private set; }
		public WorkBlock(Circulation train)
		{
			Trains = new List<Circulation>();
			Trains.Add(train);
		}
		public WorkBlock(Circulation train1, Circulation train2)
		{
			Trains = new List<Circulation>();
			Trains.Add(train1);
			Trains.Add(train2);
		}
		public override string ToString()
		{
			if (Trains.Count > 1)
				return string.Format("Bloque {0} {1}", Trains[0].ToString(), Trains[1].ToString());
			return string.Format("Single {0}", Trains[0].ToString());
		}
	}

	public class Maquinista
	{
		public string Id { get; set; }
		public List<Circulation> TrenesAsignados 
		{
			get
			{
				List<Circulation> salida = new List<Circulation>();
				foreach (WorkBlock bloque in Blocks)
				{
					foreach (Circulation tren in bloque.Trains)
					salida.Add(tren);	
				}
				return salida;
			}
		}
		public List<WorkBlock> Blocks {  get; set; } = new List<WorkBlock>();
	}

	public class PlanRestrictions
	{	
		public bool ConsumeUmpaired { get; set; } = true;
		public int MaxRefinementIterations { get; set; } = 100;
		public TimeSpan MaxPayload { get; set; } = new TimeSpan(9, 0, 0);
		public TimeSpan MaxDrivingTime { get; set; } = new TimeSpan(5, 0, 0);
		public TimeSpan MinIddleTime { get; set; } = new TimeSpan(0, 45, 0);
		public TimeSpan MaxTrainBlockBreakingTime { get; set; } = new TimeSpan(2, 0, 0); //Tiempo que consideramos aceptable para no romper un bloque.

	}
	public class PlanResult
	{
		public List<Maquinista> Schedules { get; set; } = new();
		public List<WorkBlock> Unassigned { get; set; } = new();

		public string Report
		{
			get
			{
				var sb = new StringBuilder();
				sb.AppendLine("=== ASIGNACIÓN DE MAQUINISTAS ===");
				foreach (var maq in Schedules)
				{
					sb.AppendLine($"Maquinista {maq.Id}:");
					if (maq.TrenesAsignados.Count == 0)
					{
						sb.AppendLine("  (Sin trenes asignados)");
					}
					else
					{
						foreach (var tren in maq.TrenesAsignados)
							sb.AppendLine($"  {tren}");
					}
					sb.AppendLine();
				}
				if (Unassigned != null && Unassigned.Count > 0)
				{
					sb.AppendLine("=== BLOQUES NO ASIGNADOS ===");
					foreach (var bloque in Unassigned)
						sb.AppendLine(bloque.ToString());
				}
				return sb.ToString();
			}
		}
	}

	
}
