using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.EntityFrameworkCore;
using Sapphire2025Server.Models;
using Sapphire2025Server.Models.Turnos;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Sapphire2025Server.Expert
{
    /// <summary>
    /// Esto es un importador del gráfico de excel para Jefes de Maquinistas.
    /// </summary>
    public class ExcelGraphImporter
    {
        private IConfiguration mvarConfig;
        public ExcelGraphImporter(IConfiguration config)
        {
            mvarConfig = config;
        }

        public async Task<string> ProcessExcel(List<List<AssignationCell>>? sheet, DateTime dateutc, int days, int localOffset)
        {
            StringBuilder salida = new StringBuilder();
            try
            {
                if (null == sheet) return "La hoja está vacía. No hay datos que importar.";
                WorkshiftAssignation[,] colSalida = new WorkshiftAssignation[days,sheet.Count];

                //Los datos que vamos a recibir son siempre del mismo tipo...
                //Cabecera1,dia1,dia2,...,dia-n
                //Cabecera2,dia1,dia2,...,dia-n
                //
                //Cabecera-m,dia1,dia2,...,dia-n
                //Gracias a los datos que pasamos como parámetros es posible acelerar el proceso.
                DateTime date = dateutc.AddMinutes(-localOffset);
				using (DataStorage almacen = new DataStorage(mvarConfig))
                {
                    string? auxCadena = await almacen.GetRegisterValue("ImplCol", "0");

                    int filaId = 0;
                    foreach (List<AssignationCell> fila in sheet)
                    {
                        //Sacamos los datos del Agente.
                        //El texto de la primera celda contiene un CF y el nombre del Agente.
                        //A nosotros sólo nos hace falta el CF.
                        User? agente = await getAgentFromHeader(fila[0].Text, almacen);
                        if (null != agente)
                        {
                            int maxCol = fila.Count - 1;                            
                            for (int col = 0; col < maxCol; col++)
                            {
                                AssignationCell original = fila[col + 1];
                                WorkshiftAssignation nueva = new WorkshiftAssignation();
                                if (null != original.Text)
                                {
                                    nueva.Id = Guid.NewGuid();
                                    nueva.Agent = agente.guid;
                                    nueva.Annotation = original.Comment;
                                    nueva.Assignation = getCleanAssignationString(original.Text);
                                    nueva.BgColor = manageBgColor(original.Bg);
                                    nueva.Date = date.AddDays(col);
                                    nueva.Definitive = getLastAssignation(nueva.Assignation??"");
                                    if(null!=nueva.Assignation)
                                        nueva.IsTD = nueva.Assignation.ToUpper().Contains("TD");
                                    colSalida[col, filaId] = nueva;
                                }                                                              
                            }
                        }
                        filaId++;
                    }
                }
                //Resuelvo los cambios entre Agentes.
                for (int col=0;col<days;col++)
                {
                    Dictionary<string, List<WorkshiftAssignation>> cambios = new Dictionary<string, List<WorkshiftAssignation>>();
                    for (int fila = 0;fila<sheet.Count;fila++)
                    {
                        //Los Agentes que no entran en la importación tienen el array con
                        //valores nulos... tengo que descartarlos.
                        if (null != colSalida[col,fila])
                        {
							string? auxColor = colSalida[col, fila].BgColor;
							if (auxColor != "transparent" && null != auxColor)
							{
								if (!cambios.ContainsKey(auxColor))
									cambios.Add(auxColor, new List<WorkshiftAssignation>());
								cambios[auxColor].Add(colSalida[col, fila]);
							}
						}
                    }
                    //Ya tenemos la colección de turnos a cambiar.
                    foreach(List<WorkshiftAssignation> grupo in cambios.Values)
						SolveChanges(grupo);



					/*
										foreach (List<WorkshiftAssignation> grupo in cambios.Values)
										{
											if(grupo.Count==2)
											{
												//Cambio simple.
												if (null!=grupo[0].Definitive && null != grupo[1].Definitive)
												{
													//Eliminamos la manía de Peñarrustria de poner dos agentes con
													//el mismo turno en el gráfico
													if (!(grupo[0].Definitive.Equals(grupo[1].Definitive,StringComparison.InvariantCultureIgnoreCase)))
													{
														grupo[0].SwappingAgent = grupo[1].Agent;
														grupo[1].SwappingAgent = grupo[0].Agent;
														string? turnoDefinitivo = grupo[0].Definitive;
														grupo[0].Definitive = grupo[1].Definitive;
														grupo[1].Definitive = turnoDefinitivo;
													}
												}
												else
												{
													//Fallo Peñarrustria
													int cuenta = grupo.Count;
												}
											}
											else if (grupo.Count>2)
											{
												int annotations = 0;
												foreach(WorkshiftAssignation asignacion in grupo)
												{
													if (null != asignacion.Annotation && asignacion.Annotation.Length > 0)
														annotations++;
												}
												if(annotations==grupo.Count) //Evito selecciones múltiples que no son cambios a múltiples bandas
												{
													//Cambio a tres, cuatro o más bandas.
													//Voy a crear una base de datos de turnos disponibles en el cambio
													//es una especie de "montón común"
													//Al montón común añado todas las asignaciones (no sólo la última)
													Dictionary<string, Guid> auxMontonComun = new Dictionary<string, Guid>();
													foreach (WorkshiftAssignation elemento in grupo)
													{
														if (null != elemento.Assignation)
														{
															string[] auxAsignaciones = elemento.Assignation.Split('/');
															foreach (string auxAsignacion in auxAsignaciones)
															{
																if(!(auxMontonComun.ContainsKey(auxAsignacion)))
																	auxMontonComun.Add(auxAsignacion, elemento.Agent);
															}											
														}
													}
													//Ahora voy a recorrer los mismos elementos con la anotación
													foreach (WorkshiftAssignation elemento in grupo)
													{
														foreach (string clave in auxMontonComun.Keys)
														{
															if (null != elemento.Annotation && elemento.Annotation.Length > 0)
															{
																if (elemento.Annotation.Contains(clave))
																{
																	elemento.SwappingAgent = auxMontonComun[clave];
																	elemento.Annotation = string.Format("Cambio a {0} bandas.", grupo.Count());
																	elemento.Definitive = clave;
																	auxMontonComun.Remove(clave);
																}
															}
														}
													}
												}
												else
												{
													//Detectado un grupo con repetición chunga.
													int cuenta = grupo.Count;
													salida.AppendFormat("Grupo de {0} agentes con información incompleta.", cuenta);
												}
											}
										}
					*/
				}

				//Escribo las asignaciones en la base de datos.
				using (DataStorage almacen = new DataStorage(mvarConfig))
                {
                    for (int col=0;col<days;col++)
                    {
                        //Elimino las asignaciones anteriores para esta fecha
                        await removeAssignations(date.AddDays(col), almacen);
                        for (int fila = 0; fila < sheet.Count; fila++)
                        {
                            if (null != colSalida[col,fila])
							    almacen.WorkShiftAssignations.Add(colSalida[col, fila]);
						}                            
                        await almacen.SaveChangesAsync();
                    }
                }                
            }
            catch (Exception ex)
            {
                salida.Append(string.Format("Error interno: {0}", ex.ToString()));
            }
			return salida.ToString();
		}

        private void SolveChanges(List<WorkshiftAssignation> grupo)
        {
            List<WorkshiftAssignation> entrada = new List<WorkshiftAssignation>();
            foreach(WorkshiftAssignation elemento in grupo)
            {
                if(null!=elemento.Definitive)
                    entrada.Add(elemento);
			}
            //Ahora sabemos que todos los elementos tienen asignación definitiva.
            if (entrada.Count > 2)
                SolveMultipleWayChange(entrada);
            else if (2 == entrada.Count)
				SolveTwoWayChange(entrada);
		}
		private void SolveMultipleWayChange(List<WorkshiftAssignation> grupo)
		{
            //Filtramos los nulos
            System.Diagnostics.Debug.Assert(grupo.Count > 2);
            List<WorkshiftAssignation> entrada = new List<WorkshiftAssignation>();
            foreach (WorkshiftAssignation elemento in grupo)
            {
                if (null != elemento.Definitive)
                    entrada.Add(elemento);
			}
            if (2 == entrada.Count)
                SolveTwoWayChange(entrada);
            else
            {
                //Filtramos sólo los elementos con anotación
                List<WorkshiftAssignation> anotados = new List<WorkshiftAssignation>();
				List<WorkshiftAssignation> descartados = new List<WorkshiftAssignation>();
				foreach (WorkshiftAssignation elemento in entrada)
                {
                    if (null != elemento.Annotation && elemento.Annotation.Length > 0)
                        anotados.Add(elemento);
                    else
                        descartados.Add(elemento);
				}
				if (2 == descartados.Count)
					SolveTwoWayChange(descartados);
				if (2 == anotados.Count)
                    SolveTwoWayChange(anotados);
                else
                {
                    //Creamos el montón común.
					Dictionary<string, Guid> MontonComun = new Dictionary<string, Guid>();
					foreach (WorkshiftAssignation elemento in anotados)
					{
                        foreach (string auxAsigna in elemento.Assignations)
                        {
                            if(!(MontonComun.ContainsKey(auxAsigna)))
                                MontonComun.Add(auxAsigna, elemento.Agent);
						}
					}
					//Asignamos directamente según anotaciones extrayendo del montón común.
					foreach (WorkshiftAssignation asignacion in anotados)
                    {
                        foreach(string clave in MontonComun.Keys)
                        {
                            if (asignacion.Annotation!.Contains(clave))
                            {
                                asignacion.SwappingAgent = MontonComun[clave];
                                asignacion.Definitive = clave;
                                MontonComun.Remove(clave);
                                break;
                            }
						}
					}
				}
			}
		}

		/*
                           int annotations = 0;
                            foreach(WorkshiftAssignation asignacion in grupo)
                            {
                                if (null != asignacion.Annotation && asignacion.Annotation.Length > 0)
                                    annotations++;
                            }
                            if(annotations==grupo.Count) //Evito selecciones múltiples que no son cambios a múltiples bandas
                            {
								//Cambio a tres, cuatro o más bandas.
								//Voy a crear una base de datos de turnos disponibles en el cambio
								//es una especie de "montón común"
								//Al montón común añado todas las asignaciones (no sólo la última)
								Dictionary<string, Guid> auxMontonComun = new Dictionary<string, Guid>();
								foreach (WorkshiftAssignation elemento in grupo)
								{
									if (null != elemento.Assignation)
									{
										string[] auxAsignaciones = elemento.Assignation.Split('/');
										foreach (string auxAsignacion in auxAsignaciones)
                                        {
                                            if(!(auxMontonComun.ContainsKey(auxAsignacion)))
											    auxMontonComun.Add(auxAsignacion, elemento.Agent);
										}											
									}
								}
								//Ahora voy a recorrer los mismos elementos con la anotación
								foreach (WorkshiftAssignation elemento in grupo)
								{
									foreach (string clave in auxMontonComun.Keys)
									{
										if (null != elemento.Annotation && elemento.Annotation.Length > 0)
										{
											if (elemento.Annotation.Contains(clave))
											{
												elemento.SwappingAgent = auxMontonComun[clave];
												elemento.Annotation = string.Format("Cambio a {0} bandas.", grupo.Count());
												elemento.Definitive = clave;
												auxMontonComun.Remove(clave);
											}
										}
									}
								}
							}
                            else
                            {
                                //Detectado un grupo con repetición chunga.
                                int cuenta = grupo.Count;
                                salida.AppendFormat("Grupo de {0} agentes con información incompleta.", cuenta);
                            }
        */
		private void SolveTwoWayChange(List<WorkshiftAssignation> grupo)
        {
            System.Diagnostics.Debug.Assert(grupo.Count==2);
			WorkshiftAssignation primero = grupo[0];
			WorkshiftAssignation segundo = grupo[1];
            if(null!=primero.Definitive && null!=segundo.Definitive)
            {
                //No hay cambio. Simplemente hay dos Agentes con el mismo turno.
                if (!primero.Definitive.Equals(segundo.Definitive, StringComparison.InvariantCultureIgnoreCase))
                {
					//Intercambio los turnos
					string? auxTurno = primero.Definitive;
					primero.Definitive = segundo.Definitive;
					segundo.Definitive = auxTurno;
					primero.SwappingAgent = segundo.Agent;
					segundo.SwappingAgent = primero.Agent;
				}
			}
		}




		private WorkshiftAssignation? auxGetTurnoByString(List<WorkshiftAssignation> grupo, string turnoId)
        {
            string onlyNumbers = GetOnlyNumbers(turnoId);
            string auxTurno = turnoId;
            if(onlyNumbers.Length>0)
            {
                auxTurno = onlyNumbers;
            }
                foreach (WorkshiftAssignation elemento in grupo)
                    if (auxTurno.Equals(elemento.Definitive)) return elemento;
            foreach (WorkshiftAssignation elemento in grupo)
                if (elemento.Definitive!.Contains(auxTurno)) return elemento;
            return null;
        }
        /// <summary>
        /// Saca los caracteres raros de la anotación y se queda solo con los números.
        /// </summary>
        /// <param name="rhs">Cadena de entrada</param>
        /// <returns>Cadena sólo con números</returns>
        private string GetOnlyNumbers(string rhs)
        {
			var matches = Regex.Matches(rhs, @"\d+");
			return matches.Count > 0 ? string.Join("|", matches.Select(m => m.Value)) : string.Empty;
		}

        private string manageBgColor(string? bgColor)
        {
            string salida = "transparent";            
            if(null!=bgColor && !bgColor.Equals("transparent"))
            {
				string entrada = bgColor.ToUpper();
				if (entrada.Equals("#FFFFCC")) return "transparent"; //Festivo
                if (entrada.Equals("#FFFF99")) return "transparent"; //Festivo alternativo
                if (entrada.Equals("#DCE6F2")) return "transparent"; //Vacaciones
				if (entrada.Equals("#92D050")) return "transparent"; //Turno a cubrir

				salida = bgColor;
            }
            return salida;
        }

        /// <summary>
        /// Elimina todas las asignaciones de la base de datos que tengan una fecha concreta
        /// </summary>
        /// <param name="date">La fecha a eliminar</param>
        /// <returns>True si todo ha ido bien.</returns>
        private async Task<bool> removeAssignations(DateTime date, DataStorage almacen)
        {
            try
            {
                List<WorkshiftAssignation> borrador =
                    await almacen.WorkShiftAssignations.Where(x => x.Date == date).ToListAsync();
                almacen.RemoveRange(borrador);
                await almacen.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<User?> getAgentFromHeader(string? header, DataStorage almacen)
        {
            if(null!=header && header.Length>0)
            {
                string[] encabezado = header.Split(' ');
                if(encabezado.Length>0)
                {
                    string auxCF = encabezado[0].Trim();
                    User? agente = await almacen.Users.Where(x => x.UserEnabled && x.CF == auxCF).FirstOrDefaultAsync();
                    if (null != agente) return agente;
                }               
            }
            return null;
        }

        private string? getCleanAssignationString(string? rhs)
        {
            if (string.IsNullOrEmpty(rhs)) return null;
            StringBuilder sb = new StringBuilder(rhs.Length);
            foreach(char c in rhs)
            {
                if (char.IsLetterOrDigit(c) || c == '/')
                    sb.Append(c);
            }
            return sb.ToString().ToUpper();
        }
        private string getLastAssignation(string rhs)
        {
            string salida = rhs;
            if (rhs.Contains('/'))
            {
                string[] asignaciones = rhs.Split('/');
                string ultima = asignaciones.Last();
                if (asignaciones.Length > 1)
                {
                    if (ultima.ToUpper().Contains("RJ") || ultima.ToUpper().Contains("SJ")) //Reducción de jornada / salida justificada.
                    {
                        salida = asignaciones[asignaciones.Length - 2];
                    }
                    else
                        salida = ultima;
                }
                else
                    salida = ultima;
            }
            return filterLastAssignation(salida);
        }
        private string filterLastAssignation(string rhs)
        {
            string entrada = rhs.ToUpper();
            if (entrada.Equals("N"))
                return "D"; //Gente que se niega a hacer TD.
            return entrada.Replace("TD", ""); //Quitamos TD.
        }

    }
}
