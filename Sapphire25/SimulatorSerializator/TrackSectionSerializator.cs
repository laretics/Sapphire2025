using FreeTrainSimulator.Models.Imported.ImportHandler.TrainSimulator;
using MemoryPack;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimulatorSerializator
{
    internal class TrackSectionSerializator : GenericSerializator
    {
        internal TrackSectionSerializator(string destinationPath, string contentRoot) : base(destinationPath, contentRoot) { }

        internal async override Task Execute()
        {
            await base.Execute();
            string auxPath = Path.Combine(ContentRoot, "GLOBAL", "tsection.dat");
            if(File.Exists(auxPath))
            {
                var trackSectionsModel = await TrackSectionsModelImportHandler.ImportGlobal(auxPath, CancellationToken.None);

                string fileName = Path.Combine(DestinationPath, "tsection.trs");
                var buffer = new ArrayBufferWriter<byte>();
                MemoryPackSerializer.Serialize(buffer, trackSectionsModel);
                File.WriteAllBytes(fileName, buffer.WrittenSpan.ToArray());
                Console.WriteLine("¡Serializadas las TrackSections!");
            }
            else
            {
                Console.WriteLine($"No he podido encontrar tsection.dat en {auxPath}");
            }
        }
    }
}
