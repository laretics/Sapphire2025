using Diamond.Basis;

namespace Diamond.Topo
{
	/// <summary>
	/// Tramo concreto sobre un eje en PK (metros).
	/// </summary>
	public sealed class AxisLineal : Lineal<long, LongAxis>
	{
		public AxisLineal()
			: base()
		{
		}

		public AxisLineal(long pk, long length)
			: base(pk, length)
		{
		}
	}
}
