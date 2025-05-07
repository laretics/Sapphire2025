using Microsoft.AspNetCore.Components;
using Sapphire2025.Icons.Roles;
using Sapphire2025Models;
using Sapphire2025Models.Authentication;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Sapphire2025.Storage
{
	public class AuthenticationClient:HttpClientBase
	{       
        public AuthenticationClient(HttpClient httpClient, IntStorageService intStorage): base(httpClient, intStorage, "sapphireauthentication") {}

        public async Task<bool> Logout(string tokenId)
        {
            try
            {
                //string request = composeCommand(
                //    "logout",
                //    new requestParam("tokenid", tokenId));

                BasicRequestModel question = new BasicRequestModel(await mvarIntStorage.getToken());
				string request = JsonSerializer.Serialize(question);
				HttpResponseMessage respuesta = await sendPutRequest("logout",request);
                
                return true;
            }
			catch (Exception ex)
			{
				Trace.WriteLine(ex);
			}
            return false;
		}
        public async Task<IEnumerable<UserModel>?> usersList()
        {
            Guid auxToken = await mvarIntStorage.getToken();
			BasicRequestModel question = new BasicRequestModel(auxToken);
            string jsonString = JsonSerializer.Serialize(question);

            HttpResponseMessage respuesta = await sendPutRequest("userslist", jsonString);

			return await respuesta.Content.ReadFromJsonAsync<IEnumerable<UserModel>?>();
        }

        public async Task<ExtendedUserModel?> userInfo(Guid userId)
        {
            Guid token = await mvarIntStorage.getToken();
			UserInfoRequestModel peticion = new UserInfoRequestModel(token,userId);
            string pregunta = JsonSerializer.Serialize(peticion);

            //string request = composeCommand(
            //    "userinfo",
            //    new requestParam("question", pregunta)
            //    );
            HttpResponseMessage respuesta = await sendPutRequest("userinfo",pregunta);
            return await respuesta.Content.ReadFromJsonAsync<ExtendedUserModel?>();
        }

        public async Task<bool> changeRoles(string tokenId, string userId, string enroles, string deroles)
        {
            //TODO: No es compatible la petición (GET) con el controlador (PUT)
            string request = composeCommand(
                "changeroles",
                new requestParam("tokenid",tokenId),
                new requestParam("userid",userId),
                new requestParam("enroles",enroles),
                new requestParam("deroles",deroles));

            HttpResponseMessage response = await sendGetRequest(request);
            return await response.Content.ReadFromJsonAsync<bool>();
        }

		public async Task<SessionModel?> Login(UserLoginModel data)
		{
            string jsonString = JsonSerializer.Serialize(data);
            HttpResponseMessage respuesta = await sendPutRequest("userlogin", jsonString);
            string? contenido = await respuesta.Content.ReadAsStringAsync();
            if (respuesta.IsSuccessStatusCode &&!string.IsNullOrWhiteSpace(contenido))
            {
                JsonSerializerOptions opciones = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                SessionModel? salida = JsonSerializer.Deserialize<SessionModel>(contenido, opciones);
                return salida;
			}
            else
            {
                return null; //No se ha podido autenticar el usuario
			}
            
		}
		internal async Task <HttpResponseMessage> sendModifyUser(ExtendedUserModel.UpdateUserPersonalDataMessage message)
        {
            string jsonString = JsonSerializer.Serialize(message);
            return await sendPutRequest("modifyuser", jsonString);
        }

		internal async Task<HttpResponseMessage> sendUserRolesUpdate(ExtendedUserModel.UpdateRolesChangeMessage message)
		{
			string jsonString = JsonSerializer.Serialize(message);
            return await sendPutRequest("changeroles", jsonString);
		}

        internal async Task<HttpResponseMessage> sendUserResetPassword
            (ExtendedUserModel.ResetPasswordDataMessage message)
        {
            string jsonString = JsonSerializer.Serialize(message);
            return await sendPutRequest("resetpwd", jsonString);
        }
        internal async Task<bool> isEmptyPassword (UserLoginModel data)
        {
            string jsonString = JsonSerializer.Serialize(data);
            HttpResponseMessage respuesta = await sendPutRequest("isemptypwd", jsonString);
            return await respuesta.Content.ReadFromJsonAsync<bool>();
        }

        internal async Task<bool> sendSetPassword(ExtendedUserModel.SetPasswordDataMessage model)
        {
            string jsonString = JsonSerializer.Serialize(model);
            HttpResponseMessage response = await sendPutRequest("setpwd", jsonString);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<bool>();
            return false;
        }

		/// <summary>
		/// En función de las credenciales del usuario devueltas por el sistema
		/// otorgará un determinado icono al usuario para mostrar en el diálogo de login
		/// y también en la tabla de usuarios
		/// </summary>
		/// <param name="credentials"></param>
		/// <returns></returns>
		public static RenderFragment userIcon (byte credentials, string color)
        {
            if (Utils.hasRole(credentials, Common.UserRole.Root))
                return roleIconSimple(Common.UserRole.Root, color);
            if(Utils.hasRole(credentials,Common.UserRole.Engineer))
                return roleIconSimple(Common.UserRole.Engineer, color);
            if (Utils.hasRole(credentials, Common.UserRole.Inspector))
                return roleIconSimple(Common.UserRole.Inspector,color);
            if (Utils.hasRole(credentials, Common.UserRole.Expert))
                return roleIconSimple(Common.UserRole.Expert, color);
            if (Utils.hasRole(credentials, Common.UserRole.Oficial))
                return roleIconSimple(Common.UserRole.Oficial, color);
            if (Utils.hasRole(credentials, Common.UserRole.Mechanic))
                return roleIconSimple(Common.UserRole.Mechanic,color);

            return roleIconSimple(Common.UserRole.Anonymous, color);
		}
        public static string userIconHtml(List<Common.UserRole> roles, string color)
        {
            if(roles.Contains(Common.UserRole.Root))
                return "m387-412 35-114-92-74h114l36-112 36 112h114l-93 74 35 114-92-71-93 71ZM240-40v-309q-38-42-59-96t-21-115q0-134 93-227t227-93q134 0 227 93t93 227q0 61-21 115t-59 96v309l-240-80-240 80Zm240-280q100 0 170-70t70-170q0-100-70-170t-170-70q-100 0-170 70t-70 170q0 100 70 170t170 70ZM320-159l160-41 160 41v-124q-35 20-75.5 31.5T480-240q-44 0-84.5-11.5T320-283v124Zm160-62Z";
			if (roles.Contains(Common.UserRole.Engineer))
                return "M440-480q-66 0-113-47t-47-113q0-66 47-113t113-47q66 0 113 47t47 113q0 66-47 113t-113 47Zm0-80q33 0 56.5-23.5T520-640q0-33-23.5-56.5T440-720q-33 0-56.5 23.5T360-640q0 33 23.5 56.5T440-560ZM884-20 756-148q-21 12-45 20t-51 8q-75 0-127.5-52.5T480-300q0-75 52.5-127.5T660-480q75 0 127.5 52.5T840-300q0 27-8 51t-20 45L940-76l-56 56ZM660-200q42 0 71-29t29-71q0-42-29-71t-71-29q-42 0-71 29t-29 71q0 42 29 71t71 29Zm-540 40v-111q0-34 17-63t47-44q51-26 115-44t142-18q-12 18-20.5 38.5T407-359q-60 5-107 20.5T221-306q-10 5-15.5 14.5T200-271v31h207q5 22 13.5 42t20.5 38H120Zm320-480Zm-33 400Z";
			if (roles.Contains(Common.UserRole.Inspector))
                return "M680-119q-8 0-16-2t-15-7l-120-70q-14-8-21.5-21.5T500-249v-141q0-16 7.5-29.5T529-441l120-70q7-5 15-7t16-2q8 0 15.5 2.5T710-511l120 70q14 8 22 21.5t8 29.5v141q0 16-8 29.5T830-198l-120 70q-7 4-14.5 6.5T680-119ZM400-480q-66 0-113-47t-47-113q0-66 47-113t113-47q66 0 113 47t47 113q0 66-47 113t-113 47ZM80-160v-112q0-33 17-62t47-44q51-26 115-44t141-18h14q6 0 12 2-8 18-13.5 37.5T404-360h-4q-71 0-127.5 18T180-306q-9 5-14.5 14t-5.5 20v32h252q6 21 16 41.5t22 38.5H80Zm320-400q33 0 56.5-23.5T480-640q0-33-23.5-56.5T400-720q-33 0-56.5 23.5T320-640q0 33 23.5 56.5T400-560Zm0-80Zm12 400Zm174-166 94 55 94-55-94-54-94 54Zm124 208 90-52v-110l-90 53v109Zm-150-52 90 53v-109l-90-53v109Z";
			if (roles.Contains(Common.UserRole.Expert))
                return "M240-80v-172q-57-52-88.5-121.5T120-520q0-150 105-255t255-105q125 0 221.5 73.5T827-615l52 205q5 19-7 34.5T840-360h-80v120q0 33-23.5 56.5T680-160h-80v80h-80v-160h160v-200h108l-38-155q-23-91-98-148t-172-57q-116 0-198 81t-82 197q0 60 24.5 114t69.5 96l26 24v208h-80Zm254-360Zm-54 80h80l6-50q8-3 14.5-7t11.5-9l46 20 40-68-40-30q2-8 2-16t-2-16l40-30-40-68-46 20q-5-5-11.5-9t-14.5-7l-6-50h-80l-6 50q-8 3-14.5 7t-11.5 9l-46-20-40 68 40 30q-2 8-2 16t2 16l-40 30 40 68 46-20q5 5 11.5 9t14.5 7l6 50Zm40-100q-25 0-42.5-17.5T420-520q0-25 17.5-42.5T480-580q25 0 42.5 17.5T540-520q0 25-17.5 42.5T480-460Z";
			if (roles.Contains(Common.UserRole.Oficial))
                return "M480-440q-50 0-85-35t-35-85q0-50 35-85t85-35q50 0 85 35t35 85q0 50-35 85t-85 35ZM240-40v-309q-38-42-59-96t-21-115q0-134 93-227t227-93q134 0 227 93t93 227q0 61-21 115t-59 96v309l-240-80-240 80Zm240-280q100 0 170-70t70-170q0-100-70-170t-170-70q-100 0-170 70t-70 170q0 100 70 170t170 70ZM320-159l160-41 160 41v-124q-35 20-75.5 31.5T480-240q-44 0-84.5-11.5T320-283v124Zm160-62Z";
			if (roles.Contains(Common.UserRole.Mechanic))
                return "M756-120 537-339l84-84 219 219-84 84Zm-552 0-84-84 276-276-68-68-28 28-51-51v82l-28 28-121-121 28-28h82l-50-50 142-142q20-20 43-29t47-9q24 0 47 9t43 29l-92 92 50 50-28 28 68 68 90-90q-4-11-6.5-23t-2.5-24q0-59 40.5-99.5T701-841q15 0 28.5 3t27.5 9l-99 99 72 72 99-99q7 14 9.5 27.5T841-701q0 59-40.5 99.5T701-561q-12 0-24-2t-23-7L204-120Z";
            
            //Usuario que no es nada de esto:
            return "M480-480q-66 0-113-47t-47-113q0-66 47-113t113-47q66 0 113 47t47 113q0 66-47 113t-113 47ZM160-160v-112q0-34 17.5-62.5T224-378q62-31 126-46.5T480-440q66 0 130 15.5T736-378q29 15 46.5 43.5T800-272v112H160Zm80-80h480v-32q0-11-5.5-20T700-306q-54-27-109-40.5T480-360q-56 0-111 13.5T260-306q-9 5-14.5 14t-5.5 20v32Zm240-320q33 0 56.5-23.5T560-640q0-33-23.5-56.5T480-720q-33 0-56.5 23.5T400-640q0 33 23.5 56.5T480-560Zm0-80Zm0 400Z";
		}

		public static RenderFragment roleIcon(uint roleId, string color)
        {
            if(roleId<256)
            {
                byte auxByte = (byte)roleId;
                Common.UserRole auxRole = (Common.UserRole)auxByte;
                return roleIconSimple(auxRole, color);
            }
            return builder => { };
        }
        public static RenderFragment roleIconSimple(Common.UserRole roleId, string color)
        {
            return builder =>
            {
				switch (roleId)
				{
					case Common.UserRole.Root:
						builder.OpenComponent<Icons.Roles.Admin>(0);
						builder.AddAttribute(1, "Color", color);
						builder.CloseComponent();
						break;
					case Common.UserRole.Engineer:
						builder.OpenComponent<Icons.Roles.Detective>(0);
						builder.AddAttribute(1, "Color", color);
						builder.CloseComponent();
						break;
					case Common.UserRole.Inspector:
						builder.OpenComponent<Icons.Roles.Inspector>(0);
						builder.AddAttribute(1, "Color", color);
						builder.CloseComponent();
						break;
					case Common.UserRole.Expert:
						builder.OpenComponent<Icons.Roles.Diagnoser>(0);
						builder.AddAttribute(1, "Color", color);
						builder.CloseComponent();
						break;
					case Common.UserRole.Oficial:
						builder.OpenComponent<Icons.Roles.AdvancedUser>(0);
						builder.AddAttribute(1, "Color", color);
						builder.CloseComponent();
						break;
					case Common.UserRole.Mechanic:
						builder.OpenComponent<Icons.Roles.Mechanic>(0);
						builder.AddAttribute(1, "Color", color);
						builder.CloseComponent();
						break;
					case Common.UserRole.Anonymous:
						builder.OpenComponent<Icons.Roles.User>(0);
						builder.AddAttribute(1, "Color", color);
						builder.CloseComponent();
						break;
				}
			};
        }
	}
}