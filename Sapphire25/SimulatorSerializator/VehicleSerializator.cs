using MemoryPack;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimulatorSerializator
{
    internal class VehicleSerializator:GenericSerializator
    {
        internal VehicleSerializator(string destinationPath, string contentRoot)
            : base(destinationPath, contentRoot) { }

        internal async override Task Execute()
        {
            await base.Execute();
            var auxColWagons = await FreeTrainSimulator.Models.Imported.ImportHandler.TrainSimulator.WagonReferenceModelImportHandler.ExpandWagonModels(mvarFolderModel, CancellationToken.None);
            foreach (var wagon in auxColWagons)
            {
                if (wagon == null)
                {
                    Console.WriteLine($"No se pudo obtener el modelo de vagón/locomotora.");
                    continue;
                }
                string fileName = Path.Combine(DestinationPath, $"{wagon.Id}.wgn");
                var buffer = new ArrayBufferWriter<byte>();
                MemoryPackSerializer.Serialize(buffer, wagon);
                File.WriteAllBytes(fileName, buffer.WrittenSpan.ToArray());
                Console.WriteLine($"Vagón/Locomotora serializado: {wagon.Name} ({wagon.Id})");
            }
            Console.WriteLine("¡Serialización completa de material móvil!");

        }

    }
}
