using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZafiroGmao.Data.Models
{
    /// <summary>
    /// Expediente asociado a una reparación, una campaña o un mantenimiento de un tren.
    /// </summary>
    public class ActionRecord
    {
        public ActionRecord() 
        { 
            Guid guid = Guid.NewGuid();
            Name=string.Empty;
            Comment=string.Empty;
            UserId=string.Empty;
        }
        [Key]
        public Guid Guid { get; set; } //Código del expediente
                
        //IMPORTANTE: Esta referencia es a un tren AENEAS. Pertenece por tanto a esta primera parte
        //del software.
        public Guid TrainId { get; set; } //Código del tren (AENEAS)
        public string UserId {  get; set; } //Usuario que abre el expediente
        public DateTime TimeStamp { get; set; } //Fecha y hora de apertura
        public string Name { get; set; } //Nombre asignado al expediente
        public string Comment {  get; set; } //Notas sobre el expediente
        public bool Campaign { get; set; } //Es una campaña o no
        public bool Corrective { get; set; } //Es correctivo o no
        public bool Terminated {  get; set; } //Terminación efectiva

        //TODO: Añadir documentos. Vincularlos.

        //TODO: Hacer lo mismo con fotos.
    }

    /// <summary>
    /// Operación correspondiente a la OT.
    /// </summary>
    public class ActionRecordOperation
    {
        public ActionRecordOperation()
        {
            Guid = Guid.NewGuid();
        }
        [Key]
        public Guid Guid { get; set; } //Clave primaria
        public Guid ActionRecordId { get; set; } //Referencia al expediente

        //IMPORTANTE: Esta referencia es a una PIEZA del tren, sin más relación con el tren al que pertenece
        //si en un futuro se extrae, se mete en otro tren, se desecha o se lleva al almacén.
        public Guid TrainPartId { get; set; } //(GMAO) Referencia al elemento del tren sobre el que se actúa
        public Guid ServiceParentId { get; set; } //Sólo para preventivos y campañas... es una referencia al preventivo o la campaña
        public byte Operation {  get; set; } //Suma de flags de tipo RecordOperationType
    }

    /// <summary>
    /// Registro de una intervención realizada en un expediente.
    /// Cada intervención cambia el estado del expediente.
    /// </summary>
    public class ActionRecordElement
    {
        public ActionRecordElement()
        {
            Guid = new Guid();
            UserId = string.Empty;
        }
        [Key]
        public Guid Guid { get; set; } //Clave primaria

        public Guid ActionRecordOperationId { get; set; } //Referencia de la OT
        [Display(Name ="Fecha y Hora")]
        public DateTime TimeStamp { get; set; } //Fecha y hora de la operación
        public string UserId {  get; set; } //Usuario que hace esta intervención
        public byte recordStatusInternal { get; set; } //Estado en que queda el expediente tras esta intervención
        [NotMapped]
        [Display(Name = "Estado")]
        public Common.ActionRecordStatus Status 
        { 
            get => (Common.ActionRecordStatus)recordStatusInternal;
            set => recordStatusInternal = (byte)value;
        }
    }



}
