using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2025Models.Authentication
{
	/// <summary>
	/// Contiene toda la información del modelo de usuario normal y además los datos de sesión, etc.
	/// </summary>
	public class ExtendedUserModel : UserModel
	{
		//La lista de roles contiene los índices de todos los roles a los que pertenece este usuario
		public bool TelegramEnabled { get; set; } //true si el usuario tiene habilitado el telegram
		public string TelegramRules { get; set; } //Reglas de uso del telegram
		public bool TelegramPaired { get; set; } //true si el usuario tiene el telegram emparejado
		public long TelegramId { get; set; } //Sesión de Telegram.

		public ExtendedUserModel() : base()
		{
			roles = new Dictionary<uint, RoleInfo>();
		}

		public Dictionary<uint, RoleInfo> roles { get; set; }

		public class RoleInfo
		{
			public uint roleId { get; set; }
			public string Name { get; set; } //nombre del rol
			public bool enrolled { get; set; } //El usuario actual está enrolado o no
			public string? Comment { get; set; } //Notas sobre lo que es este rol
		}
	}
		public class SetPasswordDataMessage
		{
			public string? UserName { get; set; }
			public string? Password { get; set; }
		}
		//public abstract class UpdateBase
		//{
		//	public Guid TokenId { get; set; } // Token con la autorización para hacer modificaciones en la base de datos
			
		//	public UpdateBase(Guid tokenId)
		//	{
		//		TokenId = tokenId;
		//	}
		//}
		public class CreateNewUserDataMessage :BasicRequestModel
		{
			public string? UserName { get; set; }
			public string? CF { get; set; }
			public CreateNewUserDataMessage(Guid tokenId) 
				:base(tokenId) { }
			public CreateNewUserDataMessage(): base(Guid.Empty) { }
		}

		public abstract class UpdateUserBase:BasicRequestModel
		{
			public Guid UserId { get; set; }
			public UpdateUserBase(Guid tokenId, Guid userId)
				:base(tokenId)
			{
				UserId = userId;
			}
			public UpdateUserBase() : base(Guid.Empty) { }
		}

		public class UpdateRolesChangeMessage:UpdateUserBase
		{	
			public UpdateRolesChangeMessage(Guid tokenId, Guid userId):base(tokenId,userId)
			{
				this.colEnrole = new List<uint>();
				this.colDerole = new List<uint>();
			}
			public UpdateRolesChangeMessage() : base() { }
			public List<uint> colEnrole { get; set; }
			public List<uint> colDerole { get; set; }
		}
	public class UpdateUserPersonalDataMessage : UpdateUserBase
	{
		public UpdateUserPersonalDataMessage(Guid tokenId, Guid userId) : base(tokenId, userId)
		{ }
		public UpdateUserPersonalDataMessage() : base() { }
		public bool UpdateUserStatus { get; set; } //Modifica o no los ajustes del usuario
		public string? UserName { get; set; }
		public string? CF { get; set; }
		public bool UserEnabled { get; set; }
		public string? Email { get; set; }
		public string? Phone { get; set; }
		public string? ShortPhone { get; set; }
		public bool UpdateTelegramStatus { get; set; } //Modifica o no los ajustes de Telegram
		public string? TelegramRules { get; set; }
		public bool TelegramEnabled { get; set; } //true si el usuario tiene habilitado el telegram
	}
		public class ResetPasswordDataMessage:UpdateUserBase
		{
			public ResetPasswordDataMessage(Guid tokenId, Guid userId) : base(tokenId, userId)
			{ }
			public ResetPasswordDataMessage() : base() { }

		}
	
}
