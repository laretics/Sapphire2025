using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sapphire25Server.Controllers
{
	/// <summary>
	/// Controlador para gestionar peticiones anonymous de login
	/// </summary>
	[ApiController]
	[Route ("api/PreLogin")]
	public class MontefaroLoginController:ControllerBase
	{
		[AllowAnonymous]
		[HttpPost("login")]
		public IActionResult Login()
		{
			///Lógica de autenticación.
			///
			return Unauthorized("Probando");
		}

	}
}


