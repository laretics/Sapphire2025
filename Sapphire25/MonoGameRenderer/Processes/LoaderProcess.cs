using System.Threading;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Diagnostics;

using Microsoft.Xna.Framework;

namespace Orts.ActivityRunner.Processes
{
    internal sealed class LoaderProcess : ProcessBase
    {
        public LoaderProcess(GameHost gameHost) : base(gameHost, "Loader")
        {
            Profiler.ProfilingData[ProcessType.Loader] = profiler;
        }

        public bool Finished => processState.Finished;

        /// <summary>
        /// Returns a token (copyable object) which can be queried for the cancellation (termination) of the loader.
        /// </summary>
        /// <remarks>
        /// <para>
        /// All loading code should periodically (e.g. between loading each file) check the token and exit as soon
        /// as it is cancelled (<see cref="CancellationToken.IsCancellationRequested"/>).
        /// </para>
        /// </remarks>
        public CancellationToken CancellationToken => cancellationTokenSource.Token;

        protected override void Update(GameTime gameTime)
        {
            gameHost.State?.Load().Wait();
        }
    }
}
