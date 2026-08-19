namespace Tourmaline26.Services.Correspondence
{
	/// <summary>Ordenación por instante de salida (programada o estimada).</summary>
	public interface ISortable
	{
		DateTime SortTime { get; }
	}
}
