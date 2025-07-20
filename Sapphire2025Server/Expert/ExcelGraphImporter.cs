using Microsoft.EntityFrameworkCore;
using Sapphire2025Server.Models;
using Sapphire2025Server.Models.Turnos;
using System.Text;
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

        public async Task<string> ProcessExcel(List<List<AssignationCell>>? sheet, DateTime date, int days)
        {
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
                using (DataStorage almacen = new DataStorage(mvarConfig))
                {
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
                                    nueva.Definitive = getLastAssignation(original.Text);
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
                        string? auxColor = colSalida[col, fila].BgColor;
                        if (auxColor!="transparent" && null!=auxColor)
                        {
                            if (!cambios.ContainsKey(auxColor))
                                cambios.Add(auxColor, new List<WorkshiftAssignation>());
                            cambios[auxColor].Add(colSalida[col, fila]);
                        }
                    }
                    //Ya tenemos la colección de turnos a cambiar.
                    foreach (List<WorkshiftAssignation> grupo in cambios.Values)
                    {
                        if(grupo.Count==2)
                        {
                            //Cambio normal.
                            grupo[0].SwappingAgent = grupo[1].Agent;
                            grupo[1].SwappingAgent = grupo[0].Agent;
                            string? turnoDefinitivo = grupo[0].Definitive;
                            grupo[0].Definitive = grupo[1].Definitive;
                            grupo[1].Definitive = turnoDefinitivo;
                        }
                        else if (grupo.Count>2)
                        {
                            //Cambio a tres, cuatro o más bandas.
                            Dictionary<Guid, string?> auxColTurnosOriginales = new Dictionary<Guid, string?>();
                            foreach(WorkshiftAssignation elemento in grupo)
                            {
                                //Guardo la asignación definitiva de este agente en un diccionario.
                                auxColTurnosOriginales.Add(elemento.Agent, elemento.Definitive);
                                if(null!= elemento.Annotation)
                                {
                                    //Obtengo el id de Agente que hará realmente este turno tras el cambio.
                                    WorkshiftAssignation? otro = auxGetTurnoByString(grupo, elemento.Annotation);
                                    if (null != otro)
                                        elemento.SwappingAgent = otro.Agent;
                                }                                   
                            }
                            //Asigno los turnos cambiados a los agentes en lugar del que tenían.
                            foreach(WorkshiftAssignation elemento in grupo)
                                elemento.Definitive = auxColTurnosOriginales[elemento.SwappingAgent];
                        }
                    }
                }
                //Escribo las asignaciones en la base de datos.
                using (DataStorage almacen = new DataStorage(mvarConfig))
                {
                    for (int col=0;col<days;col++)
                    {
                        //Elimino las asignaciones anteriores para esta fecha
                        await removeAssignations(date.AddDays(col), almacen);
                        for (int fila = 0; fila < sheet.Count; fila++)
                            almacen.WorkShiftAssignations.Add(colSalida[col, fila]);
                        await almacen.SaveChangesAsync();
                    }
                }
                return string.Empty;
            }
            catch (Exception ex)
            {
                return string.Format("Error interno: {0}", ex.ToString());
            }
        }

        private WorkshiftAssignation? auxGetTurnoByString(List<WorkshiftAssignation> grupo, string turnoId)
        {
            foreach (WorkshiftAssignation elemento in grupo)
                if (turnoId.Equals(elemento.Definitive)) return elemento;
            foreach (WorkshiftAssignation elemento in grupo)
                if (elemento.Definitive!.Contains(turnoId)) return elemento;
            return null;
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
                    User? agente = await almacen.Users.Where(x => x.CF == auxCF).FirstOrDefaultAsync();
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
                salida = asignaciones.Last();
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
