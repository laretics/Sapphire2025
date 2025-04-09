using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sapphire2025Server.Models
{
	[Table("SessionEvents")]
	public class SessionEvent		
	{
		[Key]
		public string Id {  get; set; }
		public string userId { get; set; } //Guid del usuario que provocó esta entrada

		public byte eventType { get; set; }
		[NotMapped]
		public sessionEventType type 
		{ 
			get => (sessionEventType)eventType;  
			set => eventType = (byte)value;
		}
		public DateTime timeSpan { get; set; }
		public string hostPoint {  get; set; }

		public SessionEvent()
		{
			Id = Guid.Empty.ToString();
			userId = Guid.Empty.ToString();
			type = sessionEventType.undefined;
			timeSpan = DateTime.Now;
			hostPoint = string.Empty;
		}

		public enum sessionEventType:byte
		{
			undefined=0,		//Evento sin describir
			login=1,			//El usuario inició sesión
			logout=2,			//El usuario cerró sesión
			sessionExpiry=3,	//La sesión abierta de un usuario expiró
			badPassword=4,		//Error de credenciales
			banned=5			//Usuario expulsado por un administrador
		}
	}
}
