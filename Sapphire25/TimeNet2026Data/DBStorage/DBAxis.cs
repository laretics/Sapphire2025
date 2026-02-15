using System.ComponentModel.DataAnnotations;

namespace TimeNet2026Data.DBStorage
{
	public class DBAxis
	{
		[Key]
		public int Id { get; set; } //Autoincremental para este eje.
		public string AxisId { get; set; } = string.Empty; //Id de TimeNet
		public int StorageId { get; set; } //Id del TopoStorage en el que lo hemos almacenado.
		public string Name { get; set; } = string.Empty;
		public string Comment { get; set; } = string.Empty;
		public string Color0 { get; set; } = "black";
		public string Color1 { get; set; } = "black";

	}
}
