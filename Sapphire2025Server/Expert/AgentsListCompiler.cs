using Microsoft.EntityFrameworkCore;
using Sapphire2025Models.Expert;
using Sapphire2025Server.Models.Turnos;
using Sapphire2025Server.Models;

namespace Sapphire2025Server.Expert
{
    /// <summary>
    /// Este compilador transforma el contenido de las tablas de la base de datos en una lista
    /// manipulable de Agentes y encabezados, lista para mostrar en el cliente.
    /// </summary>
    public class AgentsListCompiler
    {
        private IConfiguration mvarConfig { get; set; }
        public AgentsListCompiler(IConfiguration config)
        {
            mvarConfig = config;
        }
        public async Task<List<AgentAssignationsModel>> GetAssignationsContent(string tableId, DateTime assignationStart, int rowPositions)
        {
            List<AgentAssignationsModel> salida = new List<AgentAssignationsModel>();
            using (DataStorage almacen = new DataStorage(mvarConfig))
            {
                ExpertAgentsListView? tablaEntrada = 
                    await almacen.ExpertAgentsListViews.
                    Where(x => x.Name.Equals(tableId)).
                    FirstOrDefaultAsync();

                if(null!= tablaEntrada)
                {
                    List<ExpertAgentListRecord> entrada = await almacen.ExpertAgentListRecords.
                        Where(x => x.ParentId == tablaEntrada.Id).
                        OrderBy(x => x.Order).
                        ToListAsync();

                    AgentAssignationsModel nuevo;
                    //Si tiene el atributo "final", metemos un encabezado con el nombre de esta tabla.
                    if(tablaEntrada.Final)
                    {
                        nuevo = new AgentAssignationsModel();
                        nuevo.AgentRecord = new ExpertAgentListHeader(tablaEntrada.Name);
                        salida.Add(nuevo);
                    }
                    foreach (ExpertAgentListRecord registro in entrada)
                    {
                        switch(registro.Type)
                        {
                            case 0: //Agente
                                User? agente = await almacen.Users.
                                    Where(x => x.Id == registro.ElementId.ToString()).
                                    FirstOrDefaultAsync();
                                if(null!=agente)
                                {
                                    nuevo = new AgentAssignationsModel();
                                    nuevo.AgentRecord = new ExpertAgentListRecordModel(agente.guid);
                                    //Genera el contenido de las asignaciones con celdas null que luego habrá que rellenar
                                    nuevo.ColAssignations = new AgentAssignationsModel.AssignationContent[rowPositions];

                                    //Carga las asignaciones de este agente para las fechas indicadas.
                                    List<WorkshiftAssignation> auxAsignaciones = await almacen.WorkShiftAssignations.
                                        Where(x => x.Agent == agente.guid && x.Date >= assignationStart).
                                        ToListAsync();
                                    foreach (WorkshiftAssignation asigna in auxAsignaciones)
                                    {
                                        double indice0 = asigna.Date.Subtract(assignationStart).TotalDays;
                                        if(indice0<=rowPositions)
                                        {
                                            int indice = (int)indice0;
                                            AgentAssignationsModel.AssignationContent contenido = new AgentAssignationsModel.AssignationContent();
                                            contenido.AssignationsChain = asigna.Assignation;
                                            contenido.Definitive = asigna.Definitive;
                                            contenido.SwappingAgent = asigna.SwappingAgent;
                                            contenido.TD = asigna.IsTD;
                                            nuevo.ColAssignations[indice] = contenido;
                                        }
                                    }
                                    salida.Add(nuevo);
                                }
                                break;
                            case 1: //Separador
                                nuevo = new AgentAssignationsModel();
                                nuevo.AgentRecord = new ExpertAgentListSeparator();
                                salida.Add(nuevo);
                                break;
                            case 2: //Bloque de inclusión
                                //Hay que buscar la tabla a añadir.
                                ExpertAgentsListView? tabla = await almacen.ExpertAgentsListViews.
                                    Where(x => x.Id.Equals(registro.ElementId)).
                                    FirstOrDefaultAsync();
                                if (null != tabla)
                                    salida.AddRange(await GetAssignationsContent(tabla.Name,assignationStart, rowPositions));//Llamada recursiva.
                                break;
                        }
                    }
                }                
            }
            return salida;
        }
    }
}
