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
	public class ExtendedUserModel:UserModel
	{
		//La lista de roles contiene los índices de todos los roles a los que pertenece este usuario

		public ExtendedUserModel():base()
		{
			roles = new Dictionary<uint, RoleInfo>();	
		}

		public Dictionary<uint,RoleInfo> roles { get; set; }

		public class RoleInfo
		{
			public uint roleId { get; set; }
			public string Name { get; set; } //nombre del rol
			public bool enrolled { get; set; } //El usuario actual está enrolado o no
			public string? Comment { get; set; } //Notas sobre lo que es este rol
		}



		public class SetPasswordDataMessage
		{
			public string? UserName { get; set; }
			public string? Password { get; set; }
		}
		public abstract class UpdateBase
		{
			public string? TokenId { get; set; } // Token con la autorización para hacer modificaciones en la base de datos
			
			public UpdateBase(string? tokenId)
			{
				TokenId = tokenId;
			}
		}
		public class CreateNewUserDataMessage : UpdateBase
		{
			public string? UserName { get; set; }
			public string? CF { get; set; }
			public CreateNewUserDataMessage(string? tokenId) 
				:base(tokenId) { }
		}

		public abstract class UpdateUserBase:UpdateBase
		{
			public Guid UserId { get; set; }
			public UpdateUserBase(string? tokenId, Guid userId)
				:base(tokenId)
			{
				UserId = userId;
			}
		}

		public class UpdateRolesChangeMessage:UpdateUserBase
		{	
			public UpdateRolesChangeMessage(string? tokenId, Guid userId):base(tokenId,userId)
			{
				this.colEnrole = new List<uint>();
				this.colDerole = new List<uint>();
			}
			public List<uint> colEnrole { get; set; }
			public List<uint> colDerole { get; set; }
		}
		public class UpdateUserPersonalDataMessage:UpdateUserBase
		{
			public UpdateUserPersonalDataMessage(string? tokenId, Guid userId) : base(tokenId, userId)
			{ }

			public string? UserName { get; set; }
			public string? CF { get; set; }
			public string? Email { get; set; }
			public string? Phone { get; set; }
		}
		public class ResetPasswordDataMessage:UpdateUserBase
		{
			public ResetPasswordDataMessage(string? tokenId, Guid userId) : base(tokenId, userId)
			{ }

		}
	}
}
