using Diamond.Basis;

namespace Diamond.Topo
{
	/// <summary>
	/// <see cref="VectorFlex{T,TAxis}"/> concreto para cobertura en PK de un eje.
	/// </summary>
	public sealed class AxisVectorFlex : VectorFlex<long, LongAxis>
	{
		protected override Lineal<long, LongAxis> CreateLineal(long pk, long length)
		{
			return new AxisLineal(pk, length);
		}
	}
}
