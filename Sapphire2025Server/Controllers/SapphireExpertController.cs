using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sapphire2025Models;
using Sapphire2025Models.Aeneas;
using Sapphire2025Models.Authentication;
using Sapphire2025Server.Models;
using Sapphire2025Server.Telegram;
using System.Xml;

namespace Sapphire2025Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class SapphireExpertController : SapphireBaseController
    {
        public SapphireExpertController(IConfiguration configuration) : base(configuration) { }
        [HttpPost("uploadxmlworkshift")]
        public async Task<string> UploadXMLWorkShiftTemplate([FromForm] IFormFile file)
        {
            XmlDocument auxDocumento = new XmlDocument();
            try
            {
                using (Stream stream = file.OpenReadStream())
                {
                    auxDocumento.Load(stream);
                }
            }
            catch (XmlException ex)
            {
                return string.Format("Error de XML: {0}", ex.Message);
            }


                return ""; //Salida correcta.
        }

    }
}
