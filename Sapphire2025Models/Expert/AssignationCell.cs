namespace Sapphire2025Server.Expert
{
    /// <summary>
    /// Este objeto sirve para gestionar una asignación desde una celda de Excel.
    /// </summary>
    public class AssignationCell
    {
        public string? Text { get; set; } //Texto de la celda
        public string? Bg { get; set; } //Color de fondo (para cambios)
        public string? Comment { get; set; } //Anotación del usuario
    }
}
