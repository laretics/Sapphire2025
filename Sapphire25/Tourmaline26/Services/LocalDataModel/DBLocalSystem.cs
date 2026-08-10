using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tourmaline26.Services.LocalDataModel
{
	/// <summary>
	/// Configuración local del tren (una fila).
	/// </summary>
	[Table("DBLocalSystem")]
	public class DBLocalSystem
	{
		[Key]
		public Guid TrainId { get; set; }

		public string TrainName { get; set; } = string.Empty;

		/// <summary>Guid del documento de topología Diamond configurado / en uso.</summary>
		public Guid CurrentTopoId { get; set; }

		/// <summary>Plan publicado actualmente materializado en sesión.</summary>
		public Guid CurrentPublishedPlanId { get; set; }

		public DateTime LastSapphireDownload { get; set; }

		public DateTime LastAeneasSync { get; set; }

		public DateTime LastDiamondSync { get; set; }

		public DateTime LastTopoSync { get; set; }

		public DateTime LastPlanSync { get; set; }
	}
}
