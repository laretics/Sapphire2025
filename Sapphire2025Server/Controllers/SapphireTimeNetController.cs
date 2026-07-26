using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Sapphire2025Server.Comunications;
using TimeNet2026.Storage;
using TimeNet2026.ScriptCompiling;
using Sapphire2026.Data;
using TimeNet2026Data;
using System.Xml.Linq;
using TimeNet2026.Topo;
using System.Linq.Expressions;
using TimeNet2026.Models;
using TimeNet2026.Timed;
using TimeNet2026Data.Serialization;

namespace Sapphire2025Server.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class SapphireTimeNetController:SapphireBaseController
	{
		//Contenedor de TopoStorages en fase de borrador.
		public SapphireTimeNetController(IConfiguration configuration,
			IHubContext<SignalRHub> hubContext) : base(configuration, hubContext) 
		{
			
		}			

		[HttpPost("tnjsoncontent")]
		public async Task<TimeNetDataExportDto> GetJsonSerial([FromBody] Guid Token)
		{
			ITimeNetContextStorage contexto = new DataStorage(mvarConfig);
			DBSerializer serializer = new DBSerializer(contexto);
			TimeNetDataExportDto salida = await serializer.BuildExportDtoAsync();
			return salida;
		}
		[HttpPost("tnbincontent")]
		public async Task<IActionResult> GetBinSerial([FromBody] Guid Token)
		{
			ITimeNetContextStorage contexto = new DataStorage(mvarConfig);
			DBSerializer serializer = new DBSerializer(contexto);
			TimeNetDataExportDto salida = await serializer.BuildExportDtoAsync();
			byte[] buffer = DBSerializer.ToBinary(salida);
			return File(buffer,"application/x-msgpack");
		}

		[HttpPost("uploadxml")]
		public async Task<CompileResult> UploadXML([FromForm] IFormFile file)
		{
			// TODO actividad: este endpoint no recibe SessionToken; cuando lo tenga,
			// registrar sessionEventType.timeNetUploaded con addLoginRecord.
			CompileResult salida = new CompileResult();
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

			//Comprobación de que es un XML válido.
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
						if (compiler.Result.Success && null!=storage) //Intentará instalar este archivo en la base de datos.
							return await InstallTopoStorage(storage, compiler.Result);
						else
							return compiler.Result;
					case "rautatie":
						Guid auxStorageId = compiler.TopoStorageIdByRauta(root);
						OnyxStorage auxOnice = new OnyxStorage();
						ITimeNetContextStorage contexto = new DataStorage(mvarConfig);
						TopoStorage? auxStorage = await auxOnice.DeserializeTopoStorage(auxStorageId, contexto);
						if(null!=auxStorage)
						{
							if(compiler.CompileRauta(auxStorage, root))
								await auxOnice.SerializeRautatie(auxStorage, contexto);
						}							
						return compiler.Result;
					default:
						break;
				}
			}
			catch (System.Xml.XmlException ex)
			{
				salida.Success = false;
				salida.Message = string.Format("El archivo no es un XML válido: {0}", ex.Message);
			}
			return salida;
		}

		[HttpPost("deletetopostorage")]
		public async Task<CompileResult> DeleteTopoStorage([FromBody] Guid id)
		{
			// TODO actividad: sin SessionToken no se puede atribuir el borrado a un usuario.
			OnyxStorage auxOnice = new OnyxStorage();
			ITimeNetContextStorage contexto = new DataStorage(mvarConfig);
			return await auxOnice.RemoveTopoStorage(id, contexto);
		}

		[HttpGet("topostorages")]
		public async Task<IEnumerable<TopoStorageHeaderModel>> GetTopoStorages()
		{
			OnyxStorage auxOnice = new OnyxStorage();
			ITimeNetContextStorage contexto = new DataStorage(mvarConfig);
			IEnumerable<TopoStorageHeaderModel> salida = await auxOnice.DeserializeTopoStoragesHeaders(contexto);
			return salida;
		}

		private async Task<CompileResult> InstallTopoStorage(TopoStorage topo, CompileResult previousResult)
		{
			OnyxStorage auxOnice = new OnyxStorage();
			//Cargo la estructura actual desde la base de datos para tenerla en memoria.
			ITimeNetContextStorage contexto = new DataStorage(mvarConfig);
			await auxOnice.DeserializeMemory(contexto);
			if(auxOnice.Storages.ContainsKey(topo.Header.Id))
			{
				TopoStorage auxPrevia = auxOnice.Storages[topo.Header.Id];
				previousResult.Message = string.Format("TopoStorage database already contains one register named {0} from {1} installed. You're trying to install another TopoStorage named {2} from {3}. Please, uninstall it or create with other Guid.",
					auxPrevia.Header.Name, auxPrevia.Header.Author,
					topo.Header.Name, topo.Header.Author);
				previousResult.Success = false;
				return previousResult;
			}
			else
			{
				auxOnice.Storages.Add(topo.Header.Id, topo);
				await auxOnice.SerializeMemory(contexto);
				previousResult.Success = true;
				previousResult.Message = string.Format("TopoStorage named {0} from {1} is now installed.",
					topo.Header.Name, topo.Header.Author);
				return previousResult;
			}
		}		
	}
}
