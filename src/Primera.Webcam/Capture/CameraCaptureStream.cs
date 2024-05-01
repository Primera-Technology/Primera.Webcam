using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;

using Optional;

using Primera.Common.Logging;
using Primera.Webcam.Device;

namespace Primera.Webcam.Capture
{
    public enum CameraEnumerationErrors
    {
        DeviceNotFound,
        NoSourceReader,
        MediaTypeNotFound
    }

    public class CameraCaptureStream : IDisposable
    {
        private CameraCaptureStream(SourceReaderWrapper sourceReader)
        {
            SourceReader = sourceReader;

            CancelToken = new CancellationTokenSource();
            LoopThread = new Thread(StreamLoop)
            {
                IsBackground = true,
            };
            LoopThread.SetApartmentState(ApartmentState.MTA);
            LoopThread.Start();
        }

        public event EventHandler StreamClosed;

        public bool IsOpen { get; private set; }

        public Thread LoopThread { get; }

        public SourceReaderWrapper SourceReader { get; }

        private CancellationTokenSource CancelToken { get; }

        private bool CaptureFlag { get; set; } = false;

        private Bitmap NextBitmap { get; set; }

        private int SamplesToSkip { get; set; }

        public static Option<CameraCaptureStream, CameraEnumerationErrors> OpenCamera(string deviceName, MediaTypeSelector mediaType)
        {
            var maybeDevice = CameraCaptureFactory.SelectVidcapDevice(deviceName);
            return maybeDevice.WithException(CameraEnumerationErrors.DeviceNotFound)
                .FlatMap(device =>
                {
                    var maybeReader = CameraCaptureFactory.GetDefaultSourceReader(device).WithException(CameraEnumerationErrors.NoSourceReader);
                    return maybeReader.FlatMap(reader =>
                    {
                        var maybeMedia = CameraCaptureFactory.SelectMediaType(reader, mediaType).WithException(CameraEnumerationErrors.MediaTypeNotFound);

                        return maybeMedia.Map(media =>
                        {
                            reader.SetMediaType(media);
                            return new CameraCaptureStream(reader);
                        });
                    });
                });
        }

        public async Task<Option<Bitmap>> CaptureImageToBitmap(int skipSamples = 0)
        {
            if (!IsOpen)
            {
                throw new InvalidOperationException("This stream was closed. Please restart with OpenCamera()");
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
                    TracerST.Instance.Error(ExceptionMessage.Handled(e, "Failed to read sample"));
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

                    sample.Dispose();
                    return true;
                }, () => false);
            }

            StreamClosed?.Invoke(this, EventArgs.Empty);
        }
    }
}