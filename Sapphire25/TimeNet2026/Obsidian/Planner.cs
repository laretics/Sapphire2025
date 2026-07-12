using System;
using System.Collections.Generic;
using System.Text;

namespace TimeNet2026.Obsidian
{
	public class Planner
	{
		public List<WorkBlock> Blocks { get; set; } = new();

		public PlanResult Optimize(PlanRestrictions specs)
		{
			if (specs == null) throw new ArgumentNullException(nameof(specs));

			var bloques = new List<WorkBlock>(Blocks);
			bloques.Sort((a, b) => a.Trains[0].departure.CompareTo(b.Trains[0].departure));

			var schedules = new List<Maquinista>();
			var estados = new List<DriverState>();

			foreach (var bloque in bloques)
			{
				// Buscamos el maquinista con MENOR carga que pueda tomar este bloque
				var (mejorMaq, mejorEstado) = EncontrarMaquinistaMenosCargado(bloque, schedules, estados, specs);

				if (mejorMaq == null)
				{
					// Ninguno puede → creamos uno nuevo
					var nuevo = new Maquinista { Id = $"M{schedules.Count + 1}" };
					var nuevoEstado = new DriverState();
					schedules.Add(nuevo);
					estados.Add(nuevoEstado);

					Assign(bloque, nuevo, nuevoEstado, specs);
				}
				else
				{
					Assign(bloque, mejorMaq, mejorEstado, specs);
				}
			}

			return new PlanResult { Schedules = schedules, Unassigned = new List<WorkBlock>() };
		}

		private (Maquinista?, DriverState?) EncontrarMaquinistaMenosCargado(
			WorkBlock bloque, List<Maquinista> schedules, List<DriverState> estados, PlanRestrictions specs)
		{
			Maquinista? mejor = null;
			DriverState? mejorEstado = null;
			int menorCarga = int.MaxValue;

			for (int i = 0; i < schedules.Count; i++)
			{
				var maq = schedules[i];
				var estado = estados[i];

				if (CanAssign(bloque, maq, estado, specs))
				{
					int carga = maq.Blocks.Count;                    // puedes cambiar a minutos de jornada
					if (carga < menorCarga)
					{
						menorCarga = carga;
						mejor = maq;
						mejorEstado = estado;
					}
				}
			}
			return (mejor, mejorEstado);
		}

		private bool CanAssign(WorkBlock bloque, Maquinista maq, DriverState estado, PlanRestrictions specs)
		{
			var primer = bloque.Trains[0];
			var ultimo = bloque.Trains[^1];

			TimeSpan finUltimo = maq.Blocks.Count > 0
				? maq.Blocks[^1].Trains[^1].arrival : TimeSpan.Zero;

			if (primer.departure < finUltimo) return false;

			TimeSpan inicioJornada = maq.Blocks.Count > 0
				? maq.Blocks[0].Trains[0].departure : primer.departure;

			if ((ultimo.arrival - inicioJornada) > specs.MaxPayload)
				return false;

			TimeSpan gap = primer.departure - finUltimo;
			TimeSpan nuevaStreak = estado.DrivingStreak;

			if (maq.Blocks.Count == 0 || gap >= specs.MinIddleTime)
				nuevaStreak = TimeSpan.Zero;

			nuevaStreak += ultimo.arrival - primer.departure;

			if (maq.Blocks.Count > 0 && gap < specs.MinIddleTime)
				nuevaStreak += gap;

			return nuevaStreak <= specs.MaxDrivingTime;
		}

		private void Assign(WorkBlock bloque, Maquinista maq, DriverState estado, PlanRestrictions specs)
		{
			var primer = bloque.Trains[0];
			var ultimo = bloque.Trains[^1];
			TimeSpan finUltimo = maq.Blocks.Count > 0
				? maq.Blocks[^1].Trains[^1].arrival : TimeSpan.Zero;

			TimeSpan gap = primer.departure - finUltimo;

			if (maq.Blocks.Count == 0 || gap >= specs.MinIddleTime)
				estado.DrivingStreak = TimeSpan.Zero;

			estado.DrivingStreak += ultimo.arrival - primer.departure;

			if (maq.Blocks.Count > 0 && gap < specs.MinIddleTime)
				estado.DrivingStreak += gap;

			maq.Blocks.Add(bloque);
		}

		private class DriverState
		{
			public TimeSpan DrivingStreak { get; set; } = TimeSpan.Zero;
		}
	}
}
