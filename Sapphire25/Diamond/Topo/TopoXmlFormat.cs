namespace Diamond.Topo
{
	/// <summary>
	/// Formato de serialización topográfica.
	/// </summary>
	public enum TopoXmlFormat
	{
		/// <summary>
		/// Formato Onice/legacy: estación embebida en cada point (name, avr, pk, id).
		/// </summary>
		Legacy = 0,

		/// <summary>
		/// Formato canónico: catálogo &lt;stations&gt; + referencias station="id" en los points.
		/// </summary>
		Canonical = 1
	}
}
