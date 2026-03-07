using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Sapphire2025Server.Comunications;
using TimeNet2026.Storage;
using TimeNet2026.ScriptCompiling;
using Sapphire2026.Data;
using TimeNet2026Data;
using System.Xml.Linq;
using TimeNet2026.Topo;

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
			if(null==file || file.Length ==0)
			{
				salida.Success = false;
				salida.Message = "No se ha recibido ningún archivo.";
				return salida;
			}

			string xmlText;
			using (StreamReader reader = new StreamReader(file.OpenReadStream()))
			{
				xmlText = await reader.ReadToEndAsync();
			}

			//Comprobación de que es un XML váludo.
			try
			{
				XDocument xdoc = System.Xml.Linq.XDocument.Parse(xmlText);
				XElement? root = xdoc.Root;
				XMLCompiler compiler = new XMLCompiler();
				if(null==root)
				{
					salida.Success = false;
					salida.Message = "El archivo no tiene un nodo raíz.";
					return salida;
				}
				switch (root.Name.LocalName)
				{
					case "layout":
						TopoStorage? storage = compiler.CompileTopoStorage(root);
						salida = compiler.Result;
						break;
					case "rautatie":

					default:
						break;
				}
			}
			catch (System.Xml.XmlException ex)
			{
				salida.Success = false;
				salida.Message = string.Format("El arvhivo no es un XML válido: {0}", ex.Message);
			}
			return salida;
		}

	}
}
