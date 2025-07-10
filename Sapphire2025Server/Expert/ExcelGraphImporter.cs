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
        public async Task<string> ProcessExcel(List<List<AssignationCell>>? sheet, IConfiguration config)
        {
            try
            {
                if (null == sheet) return "Colección a importar vacía";
                List<List<AssignationCell>> columnas = transposeArray(sheet);
                int monthId = getMonth(columnas[0][0].Text);
                if (monthId < 1) return string.Format("Mes incorrecto: {0}", columnas[0][0].Text); //Mes incorrecto.
                using (DataStorage almacen = new DataStorage(config))
                {
                    //La primera columna es la que contiene los CF de los Maquinistas
                    for (int colId = 1; colId < columnas.Count; colId++)
                    {
                        int numDia = -1;
                        if (int.TryParse(columnas[colId][0].Text, out numDia))
                        //La primera fila debería contener el número del día.
                        {
                            DateTime auxFecha = new DateTime(DateTime.Now.Year, monthId, numDia);
                            //Lo primero que vamos a hacer es eliminar todos los registros de asignación con esta fecha.
                            if (!(await removeAssignations(auxFecha, almacen)))
                                return string.Format("Error al intentar eliminar asignaciones de turnos con la fecha {0:dd-MM-yy}.", auxFecha);
                            //Creamos una lista de asignaciones. La necesitamos para resolver cambios personales tras la importación.
                            List<WorkshiftAssignation> colAssign = new List<WorkshiftAssignation>();

                            for (int filaId = 1; filaId < columnas[colId].Count; filaId++)
                            {
                                User? agente = await getAgentFromHeader(columnas[0][filaId].Text, almacen);
                                string? assignation = getCleanAssignationString(columnas[colId][filaId].Text);
                                if (null != agente && null != assignation)
                                {
                                    WorkshiftAssignation nueva = new WorkshiftAssignation();
                                    nueva.Id = Guid.NewGuid();
                                    nueva.Agent = agente.guid;
                                    nueva.IsTD = false;
                                    nueva.Assignation = assignation;
                                    nueva.Definitive = getLastAssignation(assignation);
                                    nueva.Date = auxFecha;
                                    colAssign.Add(nueva);
                                }
                            }
                            //TODO: Resolver cambios personales a continuación.

                            //Una vez procesados los cambios personales, podemos dar de alta la lista en la base de datos
                            almacen.WorkShiftAssignations.AddRange(colAssign);
                            await almacen.SaveChangesAsync(); //Guardamos el día entero.
                        }
                        else //Hemos saltado al mes siguiente
                        {
                            monthId = getMonth(columnas[colId][0].Text);
                            if (monthId < 1)
                                return string.Format("Mes incorrecto: {0}", columnas[colId][0].Text);
                        }
                    }
                }
                return string.Empty;
            }
            catch (Exception ex)
            {
                return string.Format("Error interno: {0}", ex.ToString());
            }
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
        private List<List<AssignationCell>> transposeArray(List<List<AssignationCell>> rhs)
        {
            List<List<AssignationCell>> salida = new List<List<AssignationCell>>();
            int numColumnas = rhs.Max(x => x.Count);
            for (int c = 0; c<numColumnas;c++)
            {
                List<AssignationCell> columna = new List<AssignationCell>();
                for (int r=0;r<rhs.Count;r++)
                {
                    if (c < rhs[r].Count)
                        columna.Add(rhs[r][c]);
                }
                salida.Add(columna);
            }
            return salida;
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
        private int getMonth(string? rhs)
        {
            if (null == rhs) return -1;
            switch (rhs.ToUpper().Trim())
            {
                case "ENERO": return 1;
                case "FEBRERO": return 2;
                case "MARZO": return 3;
                case "ABRIL": return 4;
                case "MAYO": return 5;
                case "JUNIO": return 6;
                case "JULIO": return 7;
                case "AGOSTO": return 8;
                case "SEPTIEMBRE": return 9;
                case "OCTUBRE": return 10;
                case "NOVIEMBRE": return 11;
                case "DICIEMBRE": return 12;
                default: return -1;
            }
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
            if (rhs.Contains('/'))
            {
                string[] asignaciones = rhs.Split('/');
                return asignaciones.Last();
            }
            else
                return rhs;
        }
    }
}
