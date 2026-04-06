using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Tourmaline26.Components.Services.LocalDataModel
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
        public DateTime LastSapphireDownload { get; set; } //Fecha de la última sincronización con los datos de zafiro.
        public DateTime LastAeneasSync { get; set; } //Fecha de la última sincronización de partes de avería
        public DateTime LastTopoSync { get; set; }//Última actualización de los datos de topología
        public DateTime LastRautaSync { get; set; } //Última sincronización de horarios
        
    }
}
