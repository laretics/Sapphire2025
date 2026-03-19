using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Common.Logging;
using FreeTrainSimulator.Common.Native;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Imported.ImportHandler.TrainSimulator;
using FreeTrainSimulator.Models.Imported.Shim;
using FreeTrainSimulator.Models.Imported.State;
using FreeTrainSimulator.Models.Settings;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Models.Track;
using MemoryPack;
using MemoryPack.Formatters;
using Microsoft.Xna.Framework;
using Orts.ActivityRunner;
using Orts.ActivityRunner.Processes;
using Orts.ActivityRunner.Viewer3D;
using Orts.ActivityRunner.Viewer3D.Primitives;
using Orts.Formats.Msts;
using Orts.Simulation;
using Orts.Simulation.Activities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MonoGameRenderer.Processes
{
    internal sealed class GameStateTourmaline:GameState
    {
        private Simulator mvarSimulator;

        private static Viewer mvarViewer 
        { 
            get { return Program.Viewer; } 
            set { Program.Viewer = value; }
        }
        private LoadingPrimitive loading;
        private LoadingScreenPrimitive loadingScreen;
        private LoadingBarPrimitive loadingBar;
        private TimetableLoadingBarPrimitive timetableLoadingBar;
        private Matrix loadingMatrix = Matrix.Identity;

        private string[] arguments;

        public GameStateTourmaline(string[] args)
        {
            arguments = args;
        }
        protected override void Dispose(bool disposing)
        {
            loading?.Dispose();
            loadingScreen?.Dispose();
            loadingBar?.Dispose();
            timetableLoadingBar?.Dispose();

            base.Dispose(disposing);
        }

        #region Pantalla de carga
        private const int loadingSampleCount = 100;
        private string loadingDataKey;
        private string loadingDataFilePath;
        private long loadingBytesInitial;
        private DateTime loadingStart;
        private List<long> loadingBytesExpected;
        private List<long> loadingBytesActual;
        private TimeSpan loadingBytesSampleRate;
        private DateTime loadingNextSample = DateTime.MinValue;
        private float loadedPercent = -1f;

        private async ValueTask InitLoading()
        {
            loadingBytesActual = GetProcessBytesLoaded();
            string contentPath = @"C:\MSTS";

            var folderModel = new FolderModel("MSTS", contentPath, null);
            string serializedPath = Path.Combine(contentPath, @"TourmalineSerial");
            string tsectionFileName = Path.Combine(serializedPath, @"tsection.trs");
            string routeFileName = Path.Combine(serializedPath, @"Routes\SFM.trm");
            string pathFileName = Path.Combine(contentPath, @"ROUTES\SFM\paths\T31.pat");

            byte[] sectionBuffer = File.ReadAllBytes(tsectionFileName);
            var trackSectionsModel = MemoryPackSerializer.Deserialize<TrackSectionsModel>(sectionBuffer);

            byte[] routeBuffer = File.ReadAllBytes(routeFileName);
            RouteModel auxRouteModel = MemoryPackSerializer.Deserialize<RouteModel>(routeBuffer);
            auxRouteModel.SetParent(folderModel);

            RouteModelHeader routeHeader = (RouteModelHeader)auxRouteModel;
            PathModel auxPathModel = await PathModelImportHandler.Convert(pathFileName, auxRouteModel, CancellationToken.None);
            string consistFileName = Path.Combine(serializedPath, @"Trains\440.trn");
            byte[] consistBuffer = File.ReadAllBytes(consistFileName);
            var wagonSetModel = MemoryPackSerializer.Deserialize<WagonSetModel>(consistBuffer);
            wagonSetModel.SetParent(folderModel);
            ProfileUserSettingsModel userSettings = new ProfileUserSettingsModel();
            userSettings.KeyboardSettings = new ProfileKeyboardSettingsModel();
            Simulator Simulador = new Simulator(userSettings, auxRouteModel, trackSectionsModel);

            Simulador.SetExplore(
                auxPathModel,
                "440",
                TimeSpan.FromHours(12),
                SeasonType.Spring,
                WeatherType.Clear);

            loadingStart = DateTime.UtcNow;
            


        }

        private List<long> GetProcessBytesLoaded()
        {
            return new List<long>();
        }

        #endregion Pantalla de carga


        private void UpdateLoading()
        {
            
            
        }

        internal override void Update(RenderFrame frame, GameTime gameTime)
        {




            base.Update(frame, gameTime);
        }

    }
}
