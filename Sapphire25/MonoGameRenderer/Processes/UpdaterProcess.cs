using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Diagnostics;

using Microsoft.Xna.Framework;

using Orts.ActivityRunner.Viewer3D;

namespace Orts.ActivityRunner.Processes
{
    internal sealed class UpdaterProcess : ProcessBase
    {
        private RenderFrame CurrentFrame;

        public UpdaterProcess(GameHost gameHost) : base(gameHost, "Updater")
        {
            Profiler.ProfilingData[ProcessType.Updater] = profiler;
        }


        internal override void Stop()
        {
            foreach (GameComponent component in gameHost.Components)
                component.Enabled = false;
            base.Stop();
        }

        internal void TriggerUpdate(RenderFrame frame, GameTime gameTime)
        {
            CurrentFrame = frame;
            base.TriggerUpdate(gameTime);
        }

        protected override void Update(GameTime gameTime)
        {
            CurrentFrame.Clear();
            for (int i = 0; i < gameHost.Components.Count; i++)
            {
                if (gameHost.Components[i] is GameComponent gameComponent && gameComponent.Enabled)
                    gameComponent.Update(gameTime);
            }
            if (gameHost.State != null)
            {
                gameHost.State.Update(CurrentFrame, gameTime);
                CurrentFrame.Sort();
            }
        }
    }
}

