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
using static Orts.Formats.Msts.FolderStructure;
using MemoryPack;
using FreeTrainSimulator.Models.Imported.Shim;

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
            var folderModel = new FolderModel("MSTS", @"C:\MSTS", null);
            string fileName = @"C:\MSTS\TourmalineSerial\Serveis Ferroviaris de Mallorca.trm";
            RouteModel auxRouteModel;

            byte[] buffer = File.ReadAllBytes(fileName);
            auxRouteModel = MemoryPackSerializer.Deserialize<RouteModel>(buffer);
            auxRouteModel.SetParent(folderModel);

            ProfileUserSettingsModel userSettings = new ProfileUserSettingsModel();           
            Simulator Simulador = new Simulator(userSettings, auxRouteModel);
            mvarViewer = new Viewer(Simulador, Game);
            mvarViewer.Initialize();
            mvarViewer.Load();
            await base.Load();
        }
        internal override void BeginRender(RenderFrame frame)
        {
            mvarViewer?.BeginRender(frame);
        }

        protected override void Dispose(bool disposing)
        {
            ExportTestSummary(Passed, LoadTime);
            Environment.ExitCode = Passed ? 0 : 1;
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
