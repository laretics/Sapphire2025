using System.ComponentModel.DataAnnotations;
namespace TimeNet2026Data.DBStorage
{
	public class DBHeader
	{
		[Key]
		public Guid Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public string Description { get; set; } = string.Empty;
		public string Comment { get; set; } = string.Empty;
		public string License { get; set; } = string.Empty;
		public string Author { get; set; } = string.Empty;
		public DateTime FirstDate { get; set; }
		public DateTime LastDate { get; set; }
		public string Version { get; set; } = string.Empty;
		public string Bitmap { get; set; } = string.Empty;
	}
}
