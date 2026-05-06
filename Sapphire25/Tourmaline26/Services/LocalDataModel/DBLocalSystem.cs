using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Tourmaline26.Services.LocalDataModel
{
    /// <summary>
    /// Configuración local del tren a serializar.
    /// Esta tabla tiene un solo campo con toda la información que necesitamos del tren.
    /// </summary>
    [Table("DBLocalSystem")]
    public class DBLocalSystem
    {
        [Key]
        public Guid TrainId { get; set; }//Guid de este material móvil según Zafiro.
        public string TrainName { get; set; } //Nombre de esta unidad tren.
        public Guid CurrentTopoStorage { get; set; } //TopoStorage con el que trabajamos.
        public Guid CurrentRauta { get; set; } //Rauta con el que trabajamos.
        public string CurrentPlan { get; set; } = string.Empty; //Plan de explotación con el que trabajamos.
        public DateTime LastSapphireDownload { get; set; } //Fecha de la última sincronización con los datos de zafiro.
        public DateTime LastAeneasSync { get; set; } //Fecha de la última sincronización de partes de avería
        public DateTime LastTimeNetSync { get; set; } //Fecha de la última sincronización de TimeNet
        public DateTime LastTopoSync { get; set; }//Última actualización de los datos de topología
        public DateTime LastRautaSync { get; set; } //Última actualización de la sección rauta.
        public DateTime LastPlanSync{ get; set; } //Última selección del plan de explotación.        
    }
}
