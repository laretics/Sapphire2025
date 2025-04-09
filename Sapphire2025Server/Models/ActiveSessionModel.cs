using Google.Protobuf.WellKnownTypes;
using Sapphire2025Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection.Metadata.Ecma335;

namespace Sapphire2025Server.Models
{
	[Table("ActiveSessions")]
	public class ActiveSessionModel
	{
		[Key]
		public Guid Id { get; set; }
		public string UserId { get; set; }
		public DateTime Expiry { get; set; }
		public string HostIp { get; set; }
		public uint HostPort { get; set; }
		public byte Credentials { get; set; } //Flags de permisos posibles
		//[NotMapped]		
		//public Guid guid 
		//{
		//	get
		//	{
		//		Guid salida = Guid.Empty;
		//		Guid.TryParse(this.Id,out salida);
		//		return salida;
		//	}
		//	set
		//	{
		//		this.Id = value.ToString();
		//	}
		//}
		
	}
}
