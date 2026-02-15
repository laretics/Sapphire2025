using System.ComponentModel.DataAnnotations;
namespace TimeNet2026Data.DBStorage
{
	public class DBAsimilation
	{
		[Key]
		public int Id { get; set; } //Código interno.
		public int TopoStorageId { get; set; } //Referencia al almacén de topología.
		public string AsimilationId { get; set; } = string.Empty; //Referencia a nivel Onice
		public string Name { get; set; } = string.Empty;
		public string Comment { get; set; } = string.Empty;
		public string Color0 { get; set; } = "black";
		public string Color1 { get; set; } = "black";
		public int MaxSpeed { get; set; }
		public int OriginStationId { get; set; } //Referencia de SqLite

	}
}
