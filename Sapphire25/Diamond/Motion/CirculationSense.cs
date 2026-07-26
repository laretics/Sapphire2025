namespace Diamond.Motion
{
	/// <summary>
	/// Sentido de circulación sobre un eje, definido por origen y destino de la asimilación.
	/// </summary>
	public enum CirculationSense
	{
		/// <summary>
		/// PK creciente (origen.PK &lt; destino.PK).
		/// </summary>
		IncreasingPk = 1,

		/// <summary>
		/// PK decreciente (origen.PK &gt; destino.PK).
		/// </summary>
		DecreasingPk = -1
	}
}
