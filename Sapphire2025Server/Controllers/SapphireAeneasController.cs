using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sapphire2025Server.Models;
using System.Net;

namespace Sapphire2025Server.Controllers
{
	[ApiController]
	[Route("[controller]")]
	public class SapphireAeneasController:SapphireBaseController
	{
		public SapphireAeneasController(IConfiguration configuration) : base(configuration) 
		{ 
		}

		//[HttpGet("trains")]
		


	}
}
