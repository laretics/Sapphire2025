namespace Diamond.Topo
{
	/// <summary>
	/// Criterios de clasificación de estaciones para planificación (cruces, cantones, …).
	/// </summary>
	public static class StationClassification
	{
		/// <summary>
		/// Estación “principal” (frente a apeadero): el AVR contiene letras y todas están en mayúsculas.
		/// Ej.: PMI, MTX, INC → principal; jcv, cla, cos → apeadero.
		/// </summary>
		public static bool IsPrincipalStation(Station station)
		{
			if (station is null)
			{
				return false;
			}

			return IsPrincipalAvr(station.Avr);
		}

		public static bool IsPrincipalAvr(string? avr)
		{
			if (string.IsNullOrEmpty(avr))
			{
				return false;
			}

			bool hasLetter = false;
			int index = 0;
			while (index < avr.Length)
			{
				char c = avr[index];
				if (char.IsLetter(c))
				{
					hasLetter = true;
					if (!char.IsUpper(c))
					{
						return false;
					}
				}

				index++;
			}

			return hasLetter;
		}
	}
}
