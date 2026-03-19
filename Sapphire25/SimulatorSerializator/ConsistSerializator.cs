using MemoryPack;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimulatorSerializator
{
    internal class ConsistSerializator : GenericSerializator
    {
        internal ConsistSerializator(string destinationPath, string contentRoot)
            : base(destinationPath, contentRoot) { }
    
        internal async override Task Execute()
        {
            await base.Execute();
            var auxColWagonSets = await FreeTrainSimulator.Models.Imported.ImportHandler.TrainSimulator.WagonSetModelImportHandler.ExpandWagonSetModels(mvarFolderModel, CancellationToken.None);
            foreach (var wagonSet in auxColWagonSets)
            {
                if (wagonSet == null)
                {
                    Console.WriteLine($"No se pudo obtener el modelo de consist.");
                    continue;
                }
                string fileName = Path.Combine(DestinationPath, $"{wagonSet.Id}.trn");
                var buffer = new ArrayBufferWriter<byte>();
                MemoryPackSerializer.Serialize(buffer, wagonSet);
                File.WriteAllBytes(fileName, buffer.WrittenSpan.ToArray());
                Console.WriteLine($"Consist serializado: {wagonSet.Name} ({wagonSet.Id})");
            }
        }
    }
}
