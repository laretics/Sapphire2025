using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2025Models.Authentication
{
	/// <summary>
	/// Datos de un usuario para su representación en el cliente
	/// </summary>
	public class UserModelBase
	{
		public UserModelBase()
		{
			guid = Guid.Empty;
			CF = string.Empty;
			Name = string.Empty;
			CredentialKey = 0; //Por defecto este usuario NO puede hacer nada.
		}
		public Guid guid { get; set; }
		public string CF { get; set; }
		public string? Name { get; set; }
		public byte CredentialKey { get; set; } //Clave de 8 bits con 8 posibles activaciones para mostrar u ocultar componentes en la parte del cliente
	}
	public class UserModel:UserModelBase
	{
		public UserModel()
		{
			guid = Guid.Empty;
			sessionToken = Guid.Empty;					
			Email = string.Empty;
			PhoneNumber = string.Empty;
			ShortPhoneNumber = string.Empty;
			
			AccessFailedCount = 0;
		}	
		public Guid sessionToken { get; set; } //Este token certifica al usuario para entrar sin credenciales.		
		[Required(ErrorMessage = "El correo electrónico es obligatorio.")]
		[EmailAddress(ErrorMessage = "Formato de correo electrónico no válido.")]
		public string? Email { get; set; }
		public string? PhoneNumber {  get; set; }
		public string? ShortPhoneNumber { get; set; }//Extensión.
		public int AccessFailedCount {  get; set; }
		public bool NullPassword { get; set; } //Si es true, el usuario no tiene el password activo
		public bool TelegramEnabled { get; set; } //Si es true, el usuario tiene el telegram habilitado
		public bool HasTelegramId { get; set; } //Si es true, el usuario tiene el telegramId habilitado
	}
}
