using Sapphire2025Models.Authentication;
using System.Text.Json.Serialization;

namespace Sapphire2025Models.Expert
{
    /// <summary>
    /// Es una abstracción de la lista de agentes pensada para ser mostrada en el cliente.
    /// Es un modelo base de registro que se deriva de tres formas.
    /// </summary>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
    [JsonDerivedType(typeof(ExpertAgentListRecordModel), typeDiscriminator: "record")]
    [JsonDerivedType(typeof(ExpertAgentListHeader), typeDiscriminator: "header")]
    [JsonDerivedType(typeof(ExpertAgentListSeparator), typeDiscriminator: "separator")]
    public abstract class ExpertAgentListFormatModel
    {   
    }
    /// <summary>
    /// Agente del que se mostrarán sus datos.
    /// </summary>
    public class ExpertAgentListRecordModel: ExpertAgentListFormatModel
    {
        public Guid AgentId { get; set; } // Agente al que se refiere el registro
        public ExpertAgentListRecordModel(Guid id)
        {
            AgentId = id;
        }
        public ExpertAgentListRecordModel() { } //Constructor base para serialización
    }
    /// <summary>
    /// Encabezado de separación
    /// </summary>
    public class ExpertAgentListHeader: ExpertAgentListFormatModel
    {
        public string HeaderText { get; set; } // Texto del encabezado
        public ExpertAgentListHeader(string headerText)
        {
            this.HeaderText = headerText;
        }
        public ExpertAgentListHeader() { } //Constructor base para serialización
    }
    /// <summary>
    /// Separador estético entre Agentes.
    /// </summary>
    public class ExpertAgentListSeparator: ExpertAgentListFormatModel
    {
    }
}
