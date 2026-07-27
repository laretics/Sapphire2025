namespace Diamond.Timed
{
	/// <summary>
	/// Referencia simbólica a una estación tal como aparece en el script de demanda
	/// (AVR, id, nombre o texto entre comillas). Se resuelve contra un <see cref="Topo.TopoLayout"/>.
	/// </summary>
	public sealed class StationRef
	{
		private readonly string mvarText;

		public StationRef(string text)
		{
			mvarText = text ?? string.Empty;
		}

		/// <summary>
		/// Texto original del script (determinista).
		/// </summary>
		public string Text
		{
			get { return mvarText; }
		}

		public override string ToString()
		{
			return mvarText;
		}

		public override bool Equals(object? obj)
		{
			StationRef? other = obj as StationRef;
			if (other is null)
			{
				return false;
			}

			return string.Equals(mvarText, other.mvarText, System.StringComparison.Ordinal);
		}

		public override int GetHashCode()
		{
			return mvarText.GetHashCode(System.StringComparison.Ordinal);
		}
	}
}
