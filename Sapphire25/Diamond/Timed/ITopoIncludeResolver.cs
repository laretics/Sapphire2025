namespace Diamond.Timed
{
	/// <summary>
	/// Resuelve el nombre de un <c>include</c> de topología hacia un <see cref="TopoStorage"/>.
	/// En hosts con almacén (p. ej. Zafiro) devuelve entradas del catálogo remoto precargado;
	/// en herramientas de escritorio suele ser null y se usa solo el disco.
	/// </summary>
	public interface ITopoIncludeResolver
	{
		/// <summary>
		/// Intenta resolver <paramref name="logicalName"/> (tal como en el script, p. ej. toposfm227.xml).
		/// </summary>
		/// <returns>True si se encontró y <paramref name="storage"/> es usable.</returns>
		bool TryResolve(string logicalName, out TopoStorage? storage, out string? error);

		/// <summary>
		/// Texto opcional con nombres disponibles (para mensajes de error del compilador).
		/// </summary>
		string? FormatAvailableHint();
	}
}
