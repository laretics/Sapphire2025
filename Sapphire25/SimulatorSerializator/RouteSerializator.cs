using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Imported.ImportHandler.TrainSimulator;
using MemoryPack;
using SharpDX.MediaFoundation.DirectX;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimulatorSerializator
{
    internal class RouteSerializator:GenericSerializator
    {
        internal RouteSerializator(string destinationPath, string contentRoot)
            :base(destinationPath, contentRoot) { }
        

        internal async override Task Execute()
        {
            await base.Execute();            
            var auxColRoutes = await RouteModelImportHandler.ExpandRouteModels(mvarFolderModel, CancellationToken.None);
            foreach(var routeHeader in auxColRoutes)
            {
                var routeModel = await FreeTrainSimulator.Models.Handler.RouteModelHandler.GetExtended(routeHeader, CancellationToken.None);
                if(null==routeModel)
                {
                    Console.WriteLine($"No se pudo obtener el modelo extendido para: {routeHeader.Name}");
                }
                else
                {
                    string fileName = Path.Combine(DestinationPath, $"{routeModel.Id}.trm");
                    var buffer = new ArrayBufferWriter<byte>();
                    MemoryPackSerializer.Serialize(buffer, routeModel); // Serializa al buffer
                    File.WriteAllBytes(fileName, buffer.WrittenSpan.ToArray()); // Guarda el buffer en un archivo
                    Console.WriteLine($"Serializada: {routeModel.Name} ({routeModel.Id})");
                }
            }
            Console.WriteLine("¡Serialización completa de Rutas!");
        }
    }
}
