namespace Diamond.Controls.Rendering
{
	/// <summary>
	/// Escala del eje Y (espacio) del diagrama de malla.
	/// </summary>
	public enum MeshYScaleMode
	{
		/// <summary>Proporcional al PK de ruta (comportamiento clásico).</summary>
		LinearPk = 0,

		/// <summary>
		/// Puntos singulares (frontiers de limitaciones + estaciones/apeaderos)
		/// equidistantes en pantalla; interpolación lineal entre ellos.
		/// </summary>
		SteppedSingular = 1
	}
}
