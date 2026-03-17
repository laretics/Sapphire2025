using System;
using System.Threading;

namespace Orts.ActivityRunner.Processes
{
    internal sealed class ProcessState : IDisposable
    {
        public bool Finished { get; private set; }
        public bool Terminated { get; private set; }

        public string ProcessName { get; }
        private readonly ManualResetEvent startEvent = new ManualResetEvent(false);
        private readonly ManualResetEvent finishEvent = new ManualResetEvent(true);
        private readonly ManualResetEvent terminateEvent = new ManualResetEvent(false);
        private readonly WaitHandle[] startEvents;
        private readonly WaitHandle[] finishEvents;
        private bool disposedValue;

        public ProcessState(string name)
        {
            ProcessName = name;
            Finished = true;
            startEvents = new[] { startEvent, terminateEvent };
            finishEvents = new[] { finishEvent, terminateEvent };
        }

        public void SignalStart()
        {
            Finished = false;
            finishEvent.Reset();
            startEvent.Set();
        }

        public void SignalFinish()
        {
            Finished = true;
            startEvent.Reset();
            finishEvent.Set();
        }

        public void SignalTerminate()
        {
            Terminated = true;
            terminateEvent.Set();
        }

        public void WaitTillStarted()
        {
            WaitHandle.WaitAny(startEvents);
        }

        public void WaitTillFinished()
        {
            WaitHandle.WaitAny(finishEvents);
        }

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    startEvent?.Dispose();
                    finishEvent?.Dispose();
                    terminateEvent?.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
