using Org.BouncyCastle.Asn1;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection.Metadata.Ecma335;

namespace Sapphire2025Server.Models
{
	[Table("AspNetUsers")]
	public class User
	{
		[Key]
		public string Id { get; set; }
		public string CF { get; set; }
		public long TelegramId { get; set; }
		public string? UserName { get; set; }
		public string? NormalizedUserName {  get; set; }
		public string? Email { get; set; }
		public string? NormalizedEmail { get; set; }
		public bool EmailConfirmed {  get; set; }
		public string? PasswordHash { get; set; }
		public string? SecurityStamp { get; set; }
		public string? ConcurrencyStamp { get; set; }
		public string? PhoneNumber {  get; set; }
		public bool PhoneNumberConfirmed { get; set; }
		public bool TwoFactorEnabled { get; set; }
		public DateTime? LockoutEnd { get; set; }
		public bool LockoutEnabled { get; set; }
		public int AccessFailedCount {  get; set; }
		[NotMapped]
		public Guid guid 
		{ 
			get
			{
				Guid salida = Guid.Empty;
				Guid.TryParse(this.Id, out salida);
				return salida;
			}
			set
			{
				this.Id = value.ToString();
			}
		}

	}
}