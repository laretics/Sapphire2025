using BlazorBootstrap;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sapphire2025Models;
using Sapphire2025Models.Aeneas;
using Sapphire2025Models.Authentication;
using Sapphire2025Server.Models;
using Sapphire2025Server.Models.Turnos;
using Sapphire2025Server.Telegram;
using System.Diagnostics;
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
                    return await uploadXMLWorkShiftTemplate(auxDocumento);
				}
            }
            catch (XmlException ex)
            {
                return string.Format("Error de XML: {0}", ex.Message);
            }


                return ""; //Salida correcta.
        }

        internal async Task<string> uploadXMLWorkShiftTemplate(XmlDocument auxDocumento)
        {
            XmlElement? auxRaiz = auxDocumento.DocumentElement;
            if (null == auxRaiz || !auxRaiz.Name.Equals("plan"))
                return "No se ha encontrado el elemento padre <plan>";
            string? auxName = auxRaiz.Attributes["Name"]?.Value;
            if (null == auxName)
                return "El plan de explotación que está intentando dar de alta no tiene un nombre válido. Asigne el valor 'Name'.";
			DateTime? auxInicio = Sapphire2025Models.Common.parseSapphireDate
                (auxRaiz.Attributes["start"]?.Value);
			DateTime? auxFin = Sapphire2025Models.Common.parseSapphireDate         (auxRaiz.Attributes["end"]?.Value);
			if (null == auxInicio)
				return "No se ha especificado una fecha de comienzo para este proyecto de explotación. Debe usar el atributo 'start' con una fecha válida.";

			using (DataStorage almacen = new DataStorage(mvarConfig))
            {
				//Hay que procurar que este elemento nuevo sea compatible con todos los existentes en la base de datos.
				foreach (WorkShiftTemplateCollection element in await almacen.WorkShiftTemplateCollections.OrderBy(x => x.Begin).ToListAsync())
                {
                    if(element.Begin<auxInicio)
                    {
                        if(null!=element.EndDate && element.EndDate>auxInicio)
                        {
                            return string.Format("El plan de explotación que está intentando registrar es incompatible con el existente llamado '{0}', que termina el {1:dd-MM-yyyy}", element.Name, element.EndDate);
                        }
                    }
                    if (null != element.Name && element.Name.ToUpper().Equals(auxName.ToUpper()))
                        return string.Format("No puede dar de alta otro plan con el mismo nombre {0} que uno ya existente.", auxName);
                }
			}
			//Se supone que hemos pasado todos los filtros... ahora ya puedo crear el elemento.                
			return await createXMLWorkshiftTemplate(auxRaiz, auxInicio, auxFin, auxName);
		}

        internal async Task<string> createXMLWorkshiftTemplate(XmlElement auxDocumento, DateTime? inicio, DateTime? fin, string nombre)
        {
            Debug.Assert(null != inicio);
            using (DataStorage almacen = new DataStorage(mvarConfig))
            {
                //Primero genero el documento padre.
                WorkShiftTemplateCollection padre = new WorkShiftTemplateCollection();
                padre.Id = Guid.NewGuid();
                padre.Begin = (DateTime)inicio;
                padre.EndDate = fin;
                padre.Name = nombre;
                almacen.WorkShiftTemplateCollections.Add(padre);
                foreach (XmlNode seccion in auxDocumento.ChildNodes)
                {
                    if(seccion.NodeType== XmlNodeType.Element)
                    {
						switch (seccion.Name)
						{
                            case "inactive":
                                foreach (XmlNode descanso in seccion.ChildNodes)
                                {
                                    if(descanso.Name.Equals("ws"))
                                    {
                                        if (null!= descanso && null!= descanso.Attributes && null != descanso.Attributes["name"])
                                        {
											WorkShiftTemplate auxDescanso = new WorkShiftTemplate();
											auxDescanso.Active = false;
											auxDescanso.Name = descanso.Attributes["name"].Value;
                                            auxDescanso.Comment = descanso.Attributes["comment"]?.Value;
                                            auxDescanso.Color = descanso.Attributes["color"]?.Value;
                                            auxDescanso.Parent = padre.Id;
										}
                                    }
                                }
                                break;
                            case "active":
                                foreach(XmlNode trabajo in seccion.ChildNodes)
                                {
                                    if(trabajo.Name.Equals("ws"))
                                    {
                                        //TODO: Aquí me quedé.
                                        //Tengo que procesar los turnos de trabajo con
                                        //depósito puro y los turnos de trabajo con trenes.
                                    }
                                }
                                break;
						}
					}
                }
            }
			return "";
        }

	}
}
