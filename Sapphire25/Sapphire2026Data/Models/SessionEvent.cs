using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Sapphire2025Models;

namespace Sapphire2026.Data.Models
{
	[Table("SessionEvents")]
	public class SessionEvent		
	{
		[Key]
		public string Id {  get; set; }
		public string userId { get; set; } //Guid del usuario que provocó esta entrada

		public byte eventType { get; set; }
		[NotMapped]
		public Common.sessionEventType type 
		{ 
			get => (Common.sessionEventType)eventType;  
			set => eventType = (byte)value;
		}
		public DateTime timeSpan { get; set; }
		/// <summary>
		/// Origen (IP) y/o detalle de la acción. En producción puede ser VARCHAR corta;
		/// el servidor trunca al escribir (ver SapphireBaseController.TruncateHostPoint).
		/// </summary>
		[MaxLength(255)]
		public string hostPoint {  get; set; }

		public SessionEvent()
		{
			Id = Guid.Empty.ToString();
			userId = Guid.Empty.ToString();
			type = Common.sessionEventType.undefined;
			timeSpan = DateTime.Now;
			hostPoint = string.Empty;
		}

		
	}
}
