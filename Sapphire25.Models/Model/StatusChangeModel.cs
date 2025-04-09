namespace Sapphire25.Models.Model
{
	public class StatusChangeModel
	{
		public Common.TrainStatus status { get; set; }
		public DateTime timeStamp { get; set; }
		public Guid guid { get; set; } //Referencia interna de la transacción
		public string? userId { get; set; } //Referencia al usuario que ha ordenado la transacción
		public string? comment { get; set; }
	}
}
