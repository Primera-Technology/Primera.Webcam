using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;

using Optional;
using Optional.Unsafe;

using Primera.Common.Logging;
using Primera.Webcam.Device;
using Primera.Webcam.Streaming;

namespace Primera.Webcam.Capture
{
    public enum CameraEnumerationErrors
    {
        DeviceNotFound,
        NoSourceReader,
        MediaTypeNotFound,
        NotRun
    }

    public class CameraCaptureStream : IDisposable
    {
        internal CameraCaptureStream(string deviceName, SourceReaderWrapper sourceReader)
        {
            Trace.Info($"Initializing camera stream {deviceName}.");
            DeviceName = deviceName;
            SourceReader = sourceReader;

            CancelToken = new CancellationTokenSource();
            LoopThread = new Thread(StreamLoop)
            {
                IsBackground = true,
            };
            LoopThread.SetApartmentState(ApartmentState.MTA);
            LoopThread.Start();
        }

        public event EventHandler<SampleWrapper> FrameAvailable;

        public event EventHandler StreamClosed;

        public string DeviceName { get; }

        public bool IsOpen { get; private set; }

        public Thread LoopThread { get; }

        public SourceReaderWrapper SourceReader { get; }

        public ITrace Trace => CameraCaptureTracing.Trace;

        private CancellationTokenSource CancelToken { get; }

        private bool CaptureFlag { get; set; } = false;

        private Bitmap NextBitmap { get; set; }

        private int SamplesToSkip { get; set; }

        public static Option<CameraCaptureStream, CameraEnumerationErrors> OpenCamera(string deviceName, MediaTypeSelector? mediaType)
        {
            // Because COM objects are sensitive to threading apartment, we need to open the camera in an MTA thread.
            // The initiating thread may be disposed of after the camera is opened.
            using MTAThreadSynchronizer synchronizer = new();

            Option<CameraCaptureStream, CameraEnumerationErrors> closure = Option.None<CameraCaptureStream, CameraEnumerationErrors>(CameraEnumerationErrors.NotRun);

            void openInner(object state)
            {
                var maybeDevice = CameraCaptureFactory.SelectVidcapDevice(deviceName);
                if (!maybeDevice.HasValue)
                {
                    closure = Option.None<CameraCaptureStream, CameraEnumerationErrors>(CameraEnumerationErrors.DeviceNotFound);
                    return;
                }
                var device = maybeDevice.ValueOrFailure();

                var maybeReader = device.GetDefaultSourceReader();
                if (!maybeReader.HasValue)
                {
                    closure = Option.None<CameraCaptureStream, CameraEnumerationErrors>(CameraEnumerationErrors.NoSourceReader);
                    return;
                }
                var reader = maybeReader.ValueOrFailure();
                var maybeMedia = mediaType is not null
                    ? CameraCaptureFactory.SelectMediaType(reader, mediaType)
                    : reader.MediaTypes[0].Some();
                if (!maybeMedia.HasValue)
                {
                    closure = Option.None<CameraCaptureStream, CameraEnumerationErrors>(CameraEnumerationErrors.MediaTypeNotFound);
                    return;
                }
                var media = maybeMedia.ValueOrFailure();
                reader.SetMediaType(media);

                closure = reader.OpenStream().Some<CameraCaptureStream, CameraEnumerationErrors>();
            }

            synchronizer.Send(openInner, default);

            return closure;
        }

        public async Task<Option<Bitmap>> CaptureImageToBitmap(int skipSamples = 0)
        {
            if (!IsOpen)
            {
                return Option.None<Bitmap>();
            }

            SamplesToSkip = skipSamples;
            CaptureFlag = true;

            while (CaptureFlag && IsOpen)
            {
                await Task.Delay(10);
            }

            if (!IsOpen)
            {
                // The stream closed before we could capture an image
                CaptureFlag = false;
                return Option.None<Bitmap>();
            }

            if (NextBitmap is null)
            {
                throw new Exception("Failed to capture image");
            }

            return NextBitmap.Some();
        }

        /// <summary>
        /// End the streaming function and release camera resources.
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public void Dispose()
        {
            Trace.Info($"Disposing of camera stream {DeviceName}");
            CancelToken.Cancel();
            if (!LoopThread.Join(3000))
            {
                // What to do???? Abort is not supported
            }

            StreamClosed = null;
        }

        private void StreamLoop()
        {
            IsOpen = true;
            while (!CancelToken.IsCancellationRequested && IsOpen)
            {
                var maybeSample = Option.None<SampleWrapper>();

                try
                {
                    maybeSample = SourceReader.ReadSample();
                }
                catch (Exception e)
                {
                    Trace.Error(ExceptionMessage.Handled(e, "Failed to read sample"));
                }

                IsOpen = maybeSample.Match(sample =>
                {
                    if (CaptureFlag && SamplesToSkip-- <= 0)
                    {
                        sample.GetBitmap().MatchSome(bmp =>
                        {
                            NextBitmap = bmp;
                            CaptureFlag = false;
                        });
                    }
                    try
                    {
                        FrameAvailable?.Invoke(this, sample);
                    }
                    catch (Exception e)
                    {
                        Trace.Error(ExceptionMessage.Handled(e, "Failed during FrameAvailable event"));
                    }

                    sample.Dispose();
                    return true;
                }, () => false);
            }

            IsOpen = false;
            StreamClosed?.Invoke(this, EventArgs.Empty);
        }
    }
}