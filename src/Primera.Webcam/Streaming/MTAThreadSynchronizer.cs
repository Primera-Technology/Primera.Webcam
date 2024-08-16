using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Primera.Webcam.Streaming
{
    public class MTAThreadSynchronizer : SynchronizationContext, IDisposable
    {
        public MTAThreadSynchronizer()
        {
            CancelToken = new();
            TargetThread = new Thread(StartExecutionLoop)
            {
                Name = $"SourceReaderThread",
                IsBackground = true,
            };
            TargetThread.SetApartmentState(ApartmentState.MTA);
            TargetThread.Start();
        }

        public Thread TargetThread { get; private set; }

        private BlockingCollection<Action> ActionQueue { get; } = new BlockingCollection<Action>();

        private CancellationTokenSource CancelToken { get; }

        public void Dispose()
        {
            CancelToken.Cancel();
        }

        public T ExecuteOnThread<T>(Func<T> action)
        {
            T result = default;
            Send(_ => result = action(), null);
            return result;
        }

        public override void Post(SendOrPostCallback d, object state)
        {
            Action action = () => d(state);
            ActionQueue.Add(action);
        }

        public override void Send(SendOrPostCallback d, object state)
        {
            if (Thread.CurrentThread == TargetThread)
            {
                d(state);
            }
            else
            {
                var completionEvent = new ManualResetEvent(false);
                Action action = () =>
                {
                    d(state);
                    completionEvent.Set();
                };

                ActionQueue.Add(action);
                completionEvent.WaitOne();
                completionEvent.Dispose();
            }
        }

        public void StartExecutionLoop()
        {
            while (!CancelToken.IsCancellationRequested)
            {
                if (ActionQueue.TryTake(out Action action, TimeSpan.FromMilliseconds(10)))
                {
                    action();
                }
            }
        }
    }
}