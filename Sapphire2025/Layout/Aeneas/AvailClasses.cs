using Sapphire2025Models.Aeneas;

namespace Sapphire2025.Layout.Aeneas
{
	public class TrainSpan
	{
		private List<Lapse> mcolLapse = new List<Lapse>(); //Colección de lapsos
		public IEnumerable<Lapse> Lapses => mcolLapse;
		public void Add(DateTime begin, DateTime end)
		{
			if (end <= begin) return;
			mcolLapse.Add(
			new Lapse
			{
				Begin = begin,
				Duration = end - begin
			});
		}
		public TrainSpan FilterByJourney(TimeSpan begin, TimeSpan end)
		{
			TrainSpan salida = new TrainSpan();
			foreach (Lapse lapse in mcolLapse)
			{
				TrainSpan filtrado = lapse.Filter(begin, end);
				foreach (Lapse item in filtrado.Lapses)
					salida.Add(item.Begin, item.End);
			}
			return salida.Normalize();
		}
		public TrainSpan Normalize()
		{
			TrainSpan salida = new TrainSpan();

			if (mcolLapse.Count == 0)
				return salida;

			List<Lapse> ordenados = mcolLapse
				.OrderBy(x => x.Begin)
				.ToList();

			Lapse actual = new Lapse
			{
				Begin = ordenados[0].Begin,
				Duration = ordenados[0].Duration
			};

			for (int i = 1; i < ordenados.Count; i++)
			{
				Lapse siguiente = ordenados[i];

				if (siguiente.Begin <= actual.End) // solape o adyacencia
				{
					if (siguiente.End > actual.End)
						actual.End = siguiente.End;
				}
				else
				{
					salida.mcolLapse.Add(actual);
					actual = new Lapse
					{
						Begin = siguiente.Begin,
						Duration = siguiente.Duration
					};
				}
			}

			salida.mcolLapse.Add(actual);
			return salida;
		}
	}


	public class Lapse
	{
		public DateTime Begin { get; set; } //Comienzo del intervalo
		public TimeSpan Duration{ get; set; } //Duración del intervalo
		public DateTime End
		{
			get => Begin + Duration;
			set => Duration = value - Begin;
		}
		public TrainSpan Filter(TimeSpan filterBegin, TimeSpan filterEnd)
		{
			TrainSpan salida = new TrainSpan();

			// Si el intervalo horario cruza medianoche, hay que empezar un día antes
			DateTime firstDay = (filterEnd <= filterBegin)
				? Begin.Date.AddDays(-1)
				: Begin.Date;

			for (DateTime day = firstDay; day <= End.Date; day = day.AddDays(1))
			{
				DateTime windowBegin = day.Add(filterBegin);
				DateTime windowEnd = (filterEnd <= filterBegin)
					? day.AddDays(1).Add(filterEnd)
					: day.Add(filterEnd);

				DateTime cutBegin = Begin > windowBegin ? Begin : windowBegin;
				DateTime cutEnd = End < windowEnd ? End : windowEnd;

				if (cutEnd > cutBegin)
					salida.Add(cutBegin, cutEnd);
			}

			return salida;
		}
	}
}
