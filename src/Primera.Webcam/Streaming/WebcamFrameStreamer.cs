using System;
using System.Linq;
using System.Threading;

using CameraCapture.WPF.VideoCapture;

using MediaFoundation;
using MediaFoundation.Alt;
using MediaFoundation.Misc;
using MediaFoundation.ReadWrite;

using Primera.Common.Logging;
using Primera.Webcam.Device;

namespace Primera.Webcam.Streaming
{
    /// <summary>
    /// Polls a webcam stream and writes the returned frames to its collection of frame writers.
    /// When initialized, creates its own thread apartment to read and write samples from.
    /// </summary>
    public class WebcamFrameStreamer
    {
        private SourceReaderWrapper _readerObject;

        public WebcamFrameStreamer(ITrace trace)
        {
            Trace = trace;
            Synchronizer = new();
        }

        /// <summary>
        /// Whenever a sample has been read,
        /// </summary>
        public event EventHandler<SampleWrapper> SampleAvailable;

        public event EventHandler<SourceReaderWrapper> SourceReaderUpdated;

        public CancellationTokenSource CancelToken { get; set; }

        public SourceReaderWrapper ReaderObject
        {
            get => _readerObject;
            set
            {
                if (Equals(_readerObject, value)) return;

                _readerObject = value;
                SourceReaderUpdated?.Invoke(this, _readerObject);
            }
        }

        /// <summary>
        /// Most of our COM components need to be executed on the same thread to operate correctly.
        /// </summary>
        public ThreadSynchronizer Synchronizer { get; }

        public ITrace Trace { get; }

        public void CloseDevice(bool fromRunningThread)
        {
            lock (this)
            {
                Trace.Info("Closing any existing devices.");
                CancelToken?.Cancel();
                CancelToken = null;

                COMBase.SafeRelease(ReaderObject);
                ReaderObject = null;
            }
        }

        public void ReadAsyncSample()
        {
            try
            {
                if (ReaderObject is not IMFSourceReaderAsync asyncReader) return;

                asyncReader.ReadSample(
                    (int)MF_SOURCE_READER.FirstVideoStream,
                    0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero).CheckResult();
            }
            catch (HResultException e)
            {
                Trace.Error(ExceptionMessage.Handled(e, "Could not read async sample."));
            }
        }

        public void SetDevice(CaptureDeviceWrapper videoDevice)
        {
            CloseDevice(true);

            try
            {
                lock (this)
                {
                    var maybeOptions = SourceReaderOptionsWrapper.Create();
                    var maybeReader = maybeOptions.FlatMap(options =>
                    {
                        options.DisableReadWriteConverters(true);
                        return videoDevice.Activate().FlatMap(mediaSource => mediaSource.CreateSourceReader(options));
                    });

                    ReaderObject = maybeReader.Match(y => y, () => null);
                    if (ReaderObject is null) return;
                }
                var mediaTypes = ReaderObject.MediaTypes;

                foreach (var t in mediaTypes)
                {
                    var fourcc = new FourCC(t.VideoSubtype);

                    Trace.Verbose($"Compare Type found. [FourCC: {fourcc}] [Size: {t.FrameSize.Width}x{t.FrameSize.Height}]");
                }

                MediaTypeWrapper largestMediaType = mediaTypes
                    .GroupBy(m => m.VideoSubtype)
                    .Where(g => g.Key == MFMediaType.YUY2)
                    .SelectMany(g => g)
                    .OrderByDescending(m => m.FrameSize.Width)
                    .First();

                ReaderObject.SetMediaType(largestMediaType);

                CancelToken = new CancellationTokenSource();
                ReadSample();
            }
            catch (HResultException e)
            {
                Trace.Error(ExceptionMessage.Handled(e, "Could not set device for video capture"));
            }
        }

        public void ThreadsafeSetDevice(CaptureDeviceWrapper videoDevice)
        {
            // First we join on the running thread, if any, to make sure that everything has cancelled properly
            Synchronizer.Post(_ =>
            {
                SetDevice(videoDevice);
            }, null);
        }

        private void OnSampleAvailable(SampleWrapper sample)
        {
            SampleAvailable?.Invoke(this, sample);
            if (!CancelToken.IsCancellationRequested)
            {
                Synchronizer.Post(_ =>
                {
                    ReadSample();
                }, null);
            }
            else
            {
                Trace.Info("Cancellation requested. Device sample loop exiting.");
            }
        }

        private void ReadSample()
        {
            var maybeSample = ReaderObject.ReadSample();

            maybeSample.MatchSome(sample =>
            {
                OnSampleAvailable(sample);
                sample.Dispose();
            });

            Thread.Sleep(10);
        }
    }
}