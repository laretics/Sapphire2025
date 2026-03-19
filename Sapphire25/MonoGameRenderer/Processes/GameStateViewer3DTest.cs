using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Diagnostics;
using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Common.Logging;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Settings;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Orts.ActivityRunner.Viewer3D;
using Orts.Simulation;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Orts.Formats.Msts;
using FreeTrainSimulator.Models.Track;
using static Orts.Formats.Msts.FolderStructure;
using MemoryPack;
using FreeTrainSimulator.Models.Imported.Shim;
using FreeTrainSimulator.Models.Imported.ImportHandler.TrainSimulator;
using Orts.Simulation.Physics;
using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;
using FreeTrainSimulator.Models.Shim;


namespace Orts.ActivityRunner.Processes
{
    internal sealed class GameStateViewer3DTest : GameState
    {
        public bool Passed { get; set; }
        public double LoadTime { get; set; }

        private Viewer? mvarViewer;

        public GameStateViewer3DTest()
        {
        }


        internal override async Task Load()
        {
            //Game.PopState();
            string contentPath = @"C:\MSTS";
            
            var folderModel = new FolderModel("MSTS", contentPath, null);            
            string serializedPath = Path.Combine(contentPath, @"TourmalineSerial");
            string tsectionFileName = Path.Combine(serializedPath, @"tsection.trs");
            string routeFileName = Path.Combine(serializedPath, @"Routes\SFM.trm");
            string pathFileName = Path.Combine(contentPath,@"ROUTES\SFM\paths\T31.pat");

            byte[] sectionBuffer = File.ReadAllBytes(tsectionFileName);
            var trackSectionsModel = MemoryPackSerializer.Deserialize<TrackSectionsModel>(sectionBuffer);

            byte[] routeBuffer = File.ReadAllBytes(routeFileName);
            RouteModel auxRouteModel = MemoryPackSerializer.Deserialize<RouteModel>(routeBuffer);
            auxRouteModel.SetParent(folderModel);

            RouteModelHeader routeHeader = (RouteModelHeader)auxRouteModel;
            PathModel auxPathModel = await PathModelImportHandler.Convert(pathFileName, auxRouteModel, CancellationToken.None);

            //string auxPathTrackDB = Path.Combine(contentPath,@"ROUTES\SFM\SFM.tdb");
            //var tdbFile = new TrackDatabaseFile(auxPathTrackDB);
            //TrackDB trackDB = tdbFile.TrackDB;

            string consistFileName = Path.Combine(serializedPath, @"Trains\440.trn");
            byte[] consistBuffer = File.ReadAllBytes(consistFileName);
            var wagonSetModel = MemoryPackSerializer.Deserialize<WagonSetModel>(consistBuffer);
            wagonSetModel.SetParent(folderModel);

            ProfileUserSettingsModel userSettings = new ProfileUserSettingsModel();
            userSettings.KeyboardSettings = new ProfileKeyboardSettingsModel();
            Simulator Simulador = new Simulator(userSettings, auxRouteModel,trackSectionsModel);

            Simulador.SetExplore(
                auxPathModel,
                "440",
                TimeSpan.FromHours(12),
                SeasonType.Spring,
                WeatherType.Clear);

            //Simulador.Start(CancellationToken.None);
            //Simulador.InitializeTrains(CancellationToken.None);


            //mvarViewer = new Viewer(Simulador, Game);
            //mvarViewer.Initialize();
            //mvarViewer.Load();
            //await base.Load();
        }
        internal override void BeginRender(RenderFrame frame)
        {
            mvarViewer?.BeginRender(frame);
        }

        protected override void Dispose(bool disposing)
        {
            ExportTestSummary(Passed, LoadTime);
            System.Environment.ExitCode = Passed ? 0 : 1;
            base.Dispose(disposing);
        }

        private static void ExportTestSummary(bool passed, double loadTime)
        {
            // Append to CSV file in format suitable for Excel
            string summaryFileName = Path.Combine(RuntimeInfo.UserDataFolder, "TestingSummary.csv");
            LoggingTraceListener traceListener = Trace.Listeners.OfType<LoggingTraceListener>().FirstOrDefault();
            // Could fail if already opened by Excel
            try
            {
                using (StreamWriter writer = File.AppendText(summaryFileName))
                {
                    // Route, Activity, Passed, Errors, Warnings, Infos, Load Time, Frame Rate
                    writer.WriteLine($"{Simulator.Instance.RouteModel?.Name?.Replace(",", ";", StringComparison.OrdinalIgnoreCase)},{Simulator.Instance.ActivityModel?.Name?.Replace(",", ";", StringComparison.OrdinalIgnoreCase)},{(passed ? "Yes" : "No")}," +
                        $"{traceListener?.EventCount(TraceEventType.Critical) ?? 0 + traceListener?.EventCount(TraceEventType.Error) ?? 0}," +
                        $"{traceListener?.EventCount(TraceEventType.Warning) ?? 0}," +
                        $"{traceListener?.EventCount(TraceEventType.Information) ?? 0},{loadTime:F1},{MetricCollector.Instance.Metrics[SlidingMetric.FrameRate].SmoothedValue:F1}");
                }
            }
            catch (IOException) { }// Ignore any errors
            catch (ArgumentNullException) { }// Ignore any errors
        }
    }
}
