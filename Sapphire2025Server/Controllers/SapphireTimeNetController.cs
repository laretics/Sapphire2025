using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Sapphire2025Models.ScriptCompiling;
using Sapphire2025Server.Comunications;

namespace Sapphire2025Server.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class SapphireTimeNetController:SapphireBaseController
	{
		public SapphireTimeNetController(IConfiguration configuration,
			IHubContext<SignalRHub> hubContext) : base(configuration, hubContext) { }

		public async Task<XMLCompileResult> CompileXML([FromForm] IFormFile file)
		{
			XMLCompileResult salida = new XMLCompileResult();



			return salida;
		}

	}
}
