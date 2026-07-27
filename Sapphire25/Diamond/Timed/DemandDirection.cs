namespace Diamond.Timed
{
	/// <summary>
	/// Sentido del requisito de demanda.
	/// </summary>
	public enum DemandDirection
	{
		/// <summary>
		/// Solo origen → destino.
		/// </summary>
		Forward = 0,

		/// <summary>
		/// Ida y vuelta (misma cadencia en cada sentido).
		/// </summary>
		BothWays = 1
	}
}
