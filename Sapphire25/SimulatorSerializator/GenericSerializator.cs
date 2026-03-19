using FreeTrainSimulator.Models.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimulatorSerializator
{
    internal abstract class GenericSerializator
    {
        internal string DestinationPath { get; set; }
        internal string ContentRoot { get; set; }
        internal GenericSerializator(string destinationPath, string contentRoot)
        {
            DestinationPath = destinationPath;
            ContentRoot = contentRoot;
        }
        internal FolderModel mvarFolderModel;
        internal async virtual Task Execute()
        {
            mvarFolderModel = new FolderModel("MSTS", ContentRoot, null);
            Directory.CreateDirectory(DestinationPath);
        }

    }
}
