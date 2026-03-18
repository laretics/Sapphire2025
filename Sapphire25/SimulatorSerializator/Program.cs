using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Imported.ImportHandler.TrainSimulator;
using MemoryPack;
using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

string contentRoot = @"C:\MSTS";
string outputFolder = Path.Combine(contentRoot, "TourmalineSerial");

Directory.CreateDirectory(outputFolder);

FolderModel auxFolderModel = new FolderModel("MSTS", contentRoot, null);

// Expande y serializa todas las rutas como RouteModel completo
var auxColRoutes = await RouteModelImportHandler.ExpandRouteModels(auxFolderModel, CancellationToken.None);

foreach (var routeHeader in auxColRoutes)
{
    // Obtener el modelo extendido (RouteModel completo)
    var routeModel = await FreeTrainSimulator.Models.Handler.RouteModelHandler.GetExtended(routeHeader, CancellationToken.None);
    if (routeModel == null)
    {
        Console.WriteLine($"No se pudo obtener el modelo extendido para: {routeHeader.Name}");
        continue;
    }
    string fileName = Path.Combine(outputFolder, $"{routeModel.Name}.trm");
    var buffer = new ArrayBufferWriter<byte>();
    MemoryPackSerializer.Serialize(buffer, routeModel); // Serializa al buffer
    File.WriteAllBytes(fileName, buffer.WrittenSpan.ToArray()); // Guarda el buffer en un archivo
    Console.WriteLine($"Serializada: {routeModel.Name}");
}
Console.WriteLine("¡Serialización completa de modelos extendidos!");