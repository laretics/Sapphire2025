using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Sapphire2025Models.ScriptCompiling;
using Sapphire2025Server.Comunications;
using TimeNet2026.Storage;
using TimeNet2026.Topo;
using Sapphire2026.Data;
using TimeNet2026Data;

namespace Sapphire2025Server.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class SapphireTimeNetController:SapphireBaseController
	{
		//Contenedor de TopoStorages en fase de borrador.
		internal OnyxStorage Onice { get; private set; }

		public SapphireTimeNetController(IConfiguration configuration,
			IHubContext<SignalRHub> hubContext) : base(configuration, hubContext) 
		{
			ITimeNetContextStorage contexto = new DataStorage(mvarConfig);
			Onice = new OnyxStorage();

		}
		

		[HttpPost("uploadxml")]
		public async Task<XMLCompileResult> UploadXML([FromForm] IFormFile file)
		{
			XMLCompileResult salida = new XMLCompileResult();


			//Mensaje por defecto para probar la cadena de depuración.
			salida.Success = false;
			salida.Message = "Código vacío sin compilador";
			return salida;
		}

	}
}
