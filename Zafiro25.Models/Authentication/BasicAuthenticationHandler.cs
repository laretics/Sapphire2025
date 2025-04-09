//using Microsoft.AspNetCore.Authentication;
//using Microsoft.Extensions.Options;
//using Microsoft.Extensions.Logging;
//using System.Net.Http.Headers;
//using System.Security.Claims;
//using System.Text;
//using System.Text.Encodings.Web;

//namespace Zafiro25.Models.Authentication
//{
//	public class BasicAuthenticationHandler:AuthenticationHandler<AuthenticationSchemeOptions>
//	{
//        public BasicAuthenticationHandler(
//            IOptionsMonitor<AuthenticationSchemeOptions> options,
//            ILoggerFactory logger,
//            UrlEncoder enconder,
//            ISystemClock clock)
//            :base(options,logger,enconder,clock)
//        {
//        }
//        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
//        // Permitir solicitudes anónimas en el endpoint de autenticación
//        { 
//            if (Request.Path.StartsWithSegments("/api/PreLogin/login"))
//            {
//                return AuthenticateResult.NoResult();
//            }

//            if (!Request.Headers.ContainsKey("Authorization"))
//            {
//                return AuthenticateResult.Fail("Falta la cabecera de autenticación");
//            }
//            try
//            {
//                AuthenticationHeaderValue cabecera = AuthenticationHeaderValue.Parse(Request.Headers["Authorization"]);
//                byte[] credentialsBuffer = Convert.FromBase64String(cabecera.Parameter);
//                string[] credentials = Encoding.UTF8.GetString(credentialsBuffer).Split(":");
//                string userName = credentials[0];
//                string password = credentials[1];
//                if (userName.Equals("popo") && password.Equals("1234"))
//                {
//                    AuthenticationTicket auxTicket = getTicket(userName);
//                    return AuthenticateResult.Success(auxTicket);
//                }
//                else
//                {
//                    return AuthenticateResult.Fail("Usuario o contraseña incorrectos");
//                }
//            }
//            catch
//            {
//                return AuthenticateResult.Fail("Cabecera de autenticación errónea");
//            }
//        }
//        protected AuthenticationTicket getTicket(string userName)
//        {
//            Claim[] claims = new[]
//            {
//                new Claim(ClaimTypes.NameIdentifier,userName),
//                new Claim(ClaimTypes.Name,userName),
//            }; 
//            ClaimsIdentity identity = new ClaimsIdentity(claims,Scheme.Name);
//            ClaimsPrincipal principal = new ClaimsPrincipal(identity);
//            AuthenticationTicket salida = new AuthenticationTicket(principal, Scheme.Name);
//            return salida;
//        }
//	}   

//}