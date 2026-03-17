using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Models.Imported.State;
using Microsoft.Xna.Framework;
using Orts.ActivityRunner.Viewer3D;
using Orts.Simulation;
using Orts.Simulation.Activities;
using Orts.Simulation.Commanding;
using Orts.Simulation.World;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Orts.ActivityRunner.Processes
{
    internal sealed class GameStateViewer3D : GameState
    {
        private static GameStateViewer3D instance;
        internal readonly Viewer Viewer;
        private bool firstFrame = true;
        private int profileFrames;

        private double lastLoadRealTime;
        private double lastTotalRealSeconds = -1;
        private readonly double[] averageElapsedRealTime = new double[10];
        private int averageElapsedRealTimeIndex;


        public GameStateViewer3D(Viewer viewer)
        {
            Viewer = viewer ?? throw new ArgumentNullException(nameof(viewer));
            Viewer.Pause(viewer.UserSettings.PauseAtStart);
            instance = this;
        }

        internal override void BeginRender(RenderFrame frame)
        {
            // Do this here (instead of RenderProcess) because we only want to measure/time the running game.
            if (Game.UserSettings.Profiling)
                if (Game.UserSettings.ProfilingFrameCount > 0 && ++profileFrames > Game.UserSettings.ProfilingFrameCount || Game.UserSettings.ProfilingTime > 0 && Viewer?.RealTime >= Game.UserSettings.ProfilingTime)
                    Game.PopState();

            if (firstFrame)
            {
                // Turn off the 10FPS fixed-time-step and return to running as fast as we can.
                Game.IsFixedTimeStep = false;
                Game.InactiveSleepTime = TimeSpan.Zero;

                // We must create these forms on the main thread (Render) or they won't pump events correctly.

                //Program.SoundDebugForm = new SoundDebugForm(Viewer);
                //Program.SoundDebugForm.Hide();
                Viewer.SoundDebugFormEnabled = false;

                firstFrame = false;
            }
            Viewer.BeginRender(frame);
        }

        internal override void EndRender(RenderFrame frame)
        {
            Viewer.EndRender(frame);
        }

        internal override void Update(RenderFrame frame, GameTime gameTime)
        {
            double totalRealSeconds = gameTime.TotalGameTime.TotalSeconds;
            // Every 250ms, check for new things to load and kick off the loader.
            if (lastLoadRealTime + 0.25 < totalRealSeconds && Game.LoaderProcess.Finished)
            {
                lastLoadRealTime = totalRealSeconds;
                Viewer.World.LoadPrep();
                Game.LoaderProcess.TriggerUpdate(gameTime);
            }

            // The first time we update, the TotalRealSeconds will be ~time
            // taken to load everything. We'd rather not skip that far through
            // the simulation so the first time we deliberately have an
            // elapsed real and clock time of 0.0s.
            if (lastTotalRealSeconds == -1)
                lastTotalRealSeconds = totalRealSeconds;
            // We would like to avoid any large jumps in the simulation, so
            // this is a 4FPS minimum, 250ms maximum update time.
            else if (totalRealSeconds - lastTotalRealSeconds > 0.25f)
                lastTotalRealSeconds = totalRealSeconds;

            double elapsedRealTime = totalRealSeconds - lastTotalRealSeconds;
            lastTotalRealSeconds = totalRealSeconds;

            if (elapsedRealTime > 0)
            {
                // Store the elapsed real time, but also loop through overwriting any blank entries.
                do
                {
                    averageElapsedRealTime[averageElapsedRealTimeIndex] = elapsedRealTime;
                    averageElapsedRealTimeIndex = (averageElapsedRealTimeIndex + 1) % averageElapsedRealTime.Length;
                } while (averageElapsedRealTime[averageElapsedRealTimeIndex] == 0);

                // Elapsed real time is now the average.
                elapsedRealTime = 0;
                for (int i = 0; i < averageElapsedRealTime.Length; i++)
                    elapsedRealTime += averageElapsedRealTime[i] / averageElapsedRealTime.Length;
            }

            Viewer.Update(frame, elapsedRealTime);
        }

        internal override Task Load()
        {
            Viewer.Load();
            return base.Load();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Viewer.Terminate();
                Simulator.Instance.Stop();
                //Program.SoundDebugForm?.Dispose();
            }
            base.Dispose(disposing);
        }

        internal override async Task Save()
        {
            Simulator simulator = Simulator.Instance;
            //if (MultiPlayerManager.IsMultiPlayer() && !MultiPlayerManager.IsServer())
            //    return; //no save for multiplayer sessions yet

            if (ContainerHandlingStation.ActiveOperations)
            // don't save if performing a container load/unload
            {
                Simulator.Instance.Confirmer.Message(ConfirmLevel.Warning, Viewer.Catalog.GetString("Game save is not allowed during container load/unload"));
                return;
            }

            // Prefix with the activity filename so that, when resuming from the Menu.exe, we can quickly find those Saves 
            // that are likely to match the previously chosen route and activity.
            // Append the current date and time, so that each file is unique.
            // This is the "sortable" date format, ISO 8601, but with "." in place of the ":" which are not valid in filenames.
            string fileStem = simulator.SavePointName();

            instance.Viewer.PrepareSave(fileStem);
            GameSaveState saveState = await instance.Snapshot().ConfigureAwait(false);

            await GameSaveState.ToFile(Path.Combine(RuntimeInfo.UserDataFolder, fileStem + FileNameExtensions.SaveFile), saveState, CancellationToken.None).ConfigureAwait(false);

            // The Save command is the only command that doesn't take any action. It just serves as a marker.
            _ = new SaveCommand(simulator.Log, fileStem);
            simulator.Log.SaveLog(Path.Combine(RuntimeInfo.UserDataFolder, fileStem + ".replay"));

            // Copy the logfile to the save folder
            string logName = Path.Combine(RuntimeInfo.UserDataFolder, fileStem + FileNameExtensions.TextReport);

            string logFileName = RuntimeInfo.LogFile(Game.UserSettings.LogFilePath, Game.UserSettings.LogFileName);

            if (File.Exists(logFileName))
            {
                Trace.Flush();
                foreach (TraceListener listener in Trace.Listeners)
                    listener.Flush();
                File.Delete(logName);
                File.Copy(logFileName, logName);
            }
        }

        public override async ValueTask<GameSaveState> Snapshot()
        {
            Simulator simulator = Simulator.Instance;

            return new GameSaveState()
            {
                GameVersion = VersionInfo.Version,
                Route = simulator.RouteModel.Id,
                Path = simulator.PathName,
                GameTime = simulator.GameTime,
                RealSaveTime = DateTime.UtcNow,
                //MultiplayerGame = MultiPlayerManager.IsMultiPlayer(),
                InitialLocation = simulator.InitialLocation,
                PlayerLocation = simulator.Trains[0].FrontTDBTraveller.WorldLocation,
                ProfileSelections = profileSelections,

                ActivityEvaluationState = await ActivityEvaluation.Instance.Snapshot().ConfigureAwait(false),
                ViewerSaveState = await Viewer.Snapshot().ConfigureAwait(false),
                SimulatorSaveState = await Simulator.Instance.Snapshot().ConfigureAwait(false),
            };
        }
    }
}

