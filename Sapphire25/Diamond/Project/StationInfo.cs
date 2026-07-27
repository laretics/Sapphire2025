namespace Diamond.Project
{
	/// <summary>
	/// Identidad de estación en el proyecto compilado (independiente del topo vivo).
	/// </summary>
	public sealed class StationInfo
	{
		private readonly string mvarId;
		private readonly string mvarName;
		private readonly string mvarAvr;

		public StationInfo(string id, string name, string avr)
		{
			mvarId = id ?? string.Empty;
			mvarName = name ?? string.Empty;
			mvarAvr = avr ?? string.Empty;
		}

		public string Id
		{
			get { return mvarId; }
		}

		public string Name
		{
			get { return mvarName; }
		}

		/// <summary>Código corto / AVR (p. ej. PMI).</summary>
		public string Avr
		{
			get { return mvarAvr; }
		}

		/// <summary>Etiqueta preferida para documentación: AVR, si no nombre, si no id.</summary>
		public string DisplayCode
		{
			get
			{
				if (mvarAvr.Length > 0)
				{
					return mvarAvr;
				}

				if (mvarName.Length > 0)
				{
					return mvarName;
				}

				return mvarId;
			}
		}

		public override string ToString()
		{
			if (mvarAvr.Length > 0 && mvarName.Length > 0)
			{
				return mvarName + " (" + mvarAvr + ")";
			}

			return DisplayCode;
		}
	}
}
