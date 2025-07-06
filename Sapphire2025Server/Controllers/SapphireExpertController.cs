using BlazorBootstrap;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sapphire2025Models;
using Sapphire2025Models.Aeneas;
using Sapphire2025Models.Authentication;
using Sapphire2025Server.Models;
using Sapphire2025Server.Models.Turnos;
using Sapphire2025Server.Telegram;
using System.Collections;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security.Policy;
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
		[HttpGet("workshifttemplates")]
		public async Task<List<Sapphire2025Models.Expert.WorkShiftTemplateCollectionModel>> WorkShiftTemplates()
		{
			List<Sapphire2025Models.Expert.WorkShiftTemplateCollectionModel> salida = new List<Sapphire2025Models.Expert.WorkShiftTemplateCollectionModel>();
			using (DataStorage almacen = new DataStorage(mvarConfig))
            {
                foreach (WorkShiftTemplateCollection origen in await almacen.WorkShiftTemplateCollections.OrderByDescending(x => x.Begin).ToListAsync())
                {
                    Sapphire2025Models.Expert.WorkShiftTemplateCollectionModel destino = new Sapphire2025Models.Expert.WorkShiftTemplateCollectionModel();
                    destino.Id = origen.Id;
                    destino.Begin = origen.Begin;
                    destino.Name = origen.Name;
                    destino.Comment = origen.Comment;
                    destino.Collective = origen.Collective;
                    destino.Owner = origen.Owner==null?Guid.Empty:(Guid)origen.Owner;
                    salida.Add(destino);                    
                }
            }
			return salida;
		}
        [HttpPost("deleteworkshifttemplatecollection")]
        public async Task<bool> DeleteWorkShiftTemplateCollection([FromBody] Guid id)
        {
            try
            {
                using (DataStorage almacen = new DataStorage(mvarConfig))
                {
                    //Eliminamos los trenes y los depósitos
                    IEnumerable<WorkShiftContent> contenidos = await almacen.WorkShiftContents
                        .Where(x => x.ParentCollection == id)
                        .ToListAsync();
                    if (contenidos.Any())
                        almacen.WorkShiftContents.RemoveRange(contenidos);

                    //Eliminamos los turnos
                    List<WorkShiftTemplate> turnos = await almacen.WorkShiftTemplates
                        .Where(x => x.Parent == id)
                        .ToListAsync();
                    if (turnos.Any())
                        almacen.WorkShiftTemplates.RemoveRange(turnos);

                    WorkShiftTemplateCollection? coleccion = await almacen.WorkShiftTemplateCollections
                        .FirstOrDefaultAsync(x => x.Id == id);

                    if (null != coleccion)
                        almacen.WorkShiftTemplateCollections.Remove(coleccion);

                    await almacen.SaveChangesAsync();
                }
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
		internal async Task<string> uploadXMLWorkShiftTemplate(XmlDocument auxDocumento)
        {
            XmlElement? auxRaiz = auxDocumento.DocumentElement;
            if (null == auxRaiz || !auxRaiz.Name.Equals("plan"))
                return "No se ha encontrado el elemento padre <plan>";
            string? auxName = auxRaiz.Attributes["name"]?.Value;
            string? auxComment = auxRaiz.Attributes["comment"]?.Value;
            if (null == auxName)
                return "El plan de explotación que está intentando dar de alta no tiene un nombre válido. Asigne el valor 'Name'.";
			DateTime? auxInicio = Sapphire2025Models.Common.parseSapphireDate
                (auxRaiz.Attributes["start"]?.Value);
            string? auxAuthor = auxRaiz.Attributes["author"]?.Value;
            if (null == auxAuthor)
                return "No se ha especificado un autor para este documento. Es necesario aportar la identificación (CF) de la persona que ha creado esta colección de turnos en el sistema.";
			if (null == auxInicio)
				return "No se ha especificado una fecha de comienzo para este proyecto de explotación. Debe usar el atributo 'start' con una fecha válida.";


            User? auxUser = null;
			using (DataStorage almacen = new DataStorage(mvarConfig))
            {
                //Recuperamos la información del usuario:
                auxUser = await almacen.Users.Where(x => x.CF.Equals((string)auxAuthor)).FirstOrDefaultAsync();
                if (null == auxUser)
                    return string.Format("El usuario con CF {0} no existe en el sistema. Por favor, aporte un usuario válido y con permisos de administración.", auxAuthor);
				//Hay que procurar que este elemento nuevo sea compatible con todos los existentes en la base de datos.
				foreach (WorkShiftTemplateCollection element in await almacen.WorkShiftTemplateCollections.OrderBy(x => x.Begin).ToListAsync())
                {
                    if (null != element.Name && element.Name.ToUpper().Equals(auxName.ToUpper()))
                        return string.Format("No puede dar de alta otro plan con el mismo nombre {0} que uno ya existente.", auxName);
                }
			}
			//Se supone que hemos pasado todos los filtros... ahora ya puedo crear el elemento.                
			return await createXMLWorkshiftTemplate(auxRaiz, auxInicio,  auxName, auxComment, (User)auxUser);
		}

        internal async Task<string> createXMLWorkshiftTemplate(XmlElement auxDocumento, DateTime? inicio, string nombre,string? comment, User user)
        {
            Debug.Assert(null != inicio);
            using (DataStorage almacen = new DataStorage(mvarConfig))
            {
                //Primero genero el documento padre.
                WorkShiftTemplateCollection padre = new WorkShiftTemplateCollection();
                padre.Id = Guid.NewGuid();
                padre.Begin = (DateTime)inicio;
                padre.Name = nombre;
                padre.Comment = comment;
                padre.Owner = user.guid;
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
                                            almacen.WorkShiftTemplates.Add(auxDescanso);
										}
                                    }
                                }
                                break;
                            case "active":
                                foreach(XmlNode trabajo in seccion.ChildNodes)
                                {
                                    if(trabajo.Name.Equals("ws"))
                                    {                                        
                                        if(null!=trabajo && null!=trabajo.Attributes && null != trabajo.Attributes["name"])
                                        {
                                            string auxName = trabajo.Attributes["name"].Value == null ? "[Sin Nombre]" : trabajo.Attributes["name"].Value;
											TimeSpan? auxComienzo = Common.parseSapphireTimeSpan(trabajo.Attributes["start"]?.Value);
											TimeSpan? auxDuracion = Common.parseSapphireTimeSpan(trabajo.Attributes["duration"]?.Value);
											string? auxComment = trabajo.Attributes["comment"]?.Value;
											string? auxColor = trabajo.Attributes["color"]?.Value;
											string? auxDepot = trabajo.Attributes["depot"]?.Value;
                                            if(null!=auxComienzo && null!=auxDuracion && null!=auxColor)
                                            {
												WorkShiftTemplate auxTrabajo = new WorkShiftTemplate();
												auxTrabajo.Active = true;
												auxTrabajo.Name = auxName;
                                                auxTrabajo.Comment = auxComment;
                                                auxTrabajo.StartTime = (TimeSpan)auxComienzo;
                                                auxTrabajo.Duration = (TimeSpan)auxDuracion;
                                                auxTrabajo.Color = (string)auxColor;
                                                auxTrabajo.Att = (null != auxDepot && auxDepot.ToUpper().Contains("T"));
                                                auxTrabajo.Id = Guid.NewGuid();
                                                auxTrabajo.Parent = padre.Id;
                                                
                                                almacen.WorkShiftTemplates.Add(auxTrabajo);
                                                loadWorkSheetContents(trabajo, auxTrabajo.Id, padre.Id,almacen);
										    }                                                                                  
										}
                                    }
                                }
                                break;
						}
					}
                }
                if (await almacen.SaveChangesAsync() < 1)
                    return "Por algún motivo no se ha guardado ningún cambio en la base de datos.";                    
            }
			return "";
        }

        private void loadWorkSheetContents(XmlNode parent, Guid parentId, Guid rootId, DataStorage almacen)
        {
            foreach (XmlNode hijo in parent.ChildNodes)
            {
                if(hijo.NodeType== XmlNodeType.Element)
                {
                    TimeSpan? auxBegin;
                    TimeSpan? auxEnd;
                    switch(hijo.Name)
                    {
                        case "train":
                            auxBegin = Common.parseSapphireTimeSpan(hijo.Attributes["start"]?.Value);
                            auxEnd = Common.parseSapphireTimeSpan(hijo.Attributes["end"]?.Value);
                            string? auxName = hijo.Attributes["id"]?.Value;
                            bool auxDisc = (null != hijo.Attributes["disc"] && hijo.Attributes["disc"].Value.ToUpper().Contains("T"));
                            if(null!=auxBegin && null!=auxEnd && null!=auxName)
                            {
                                WorkShiftContent tren = new WorkShiftContent();
                                tren.Begin = (TimeSpan)auxBegin;
                                tren.Duration = ((TimeSpan)auxEnd).Subtract(tren.Begin);
                                tren.Id = Guid.NewGuid();
                                tren.TrainId = auxName;
                                tren.Parent = parentId;
                                tren.ParentCollection = rootId;
                                tren.ContentType = 1;
                                tren.Discrectional = auxDisc;
                                almacen.WorkShiftContents.Add(tren);
                            }
                            break;

                        case "depot":
							auxBegin = Common.parseSapphireTimeSpan(hijo.Attributes["start"]?.Value);
							auxEnd = Common.parseSapphireTimeSpan(hijo.Attributes["end"]?.Value);
                            bool auxForeign = (null != hijo.Attributes["foreign"] && hijo.Attributes["foreign"].Value.ToUpper().Contains("T"));
                            if(null!=auxBegin && null!=auxEnd)
                            {
								WorkShiftContent deposito = new WorkShiftContent();
								deposito.Begin = (TimeSpan)auxBegin;
                                deposito.Duration = ((TimeSpan)auxEnd).Subtract(deposito.Begin);
                                deposito.Id = Guid.NewGuid();
                                deposito.Parent = parentId;
                                deposito.ParentCollection = rootId;
                                deposito.ContentType = 0;
                                deposito.Foreign = auxForeign;
                                almacen.WorkShiftContents.Add(deposito);
							}
							break;
                    }
                }
            }
        }
	}
}
