using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using CameraCapture.WPF.VideoCapture;

using MediaFoundation;
using MediaFoundation.Misc;

using Primera.Common.Logging;
using Primera.Webcam.Device;

using WPFImage = System.Windows.Controls.Image;

namespace Primera.Webcam.Streaming
{
    /// <summary>
    /// Writes passed in frames to a given <see cref="System.Windows.Controls.Image"/>
    /// </summary>
    public class WpfImageSampleSink : ISampleSink
    {
        public WpfImageSampleSink(WPFImage writeTo)
        {
            WriteTo = writeTo;
        }

        /// <summary>
        /// If not null, this bitmap target is assignd to the given Image Source
        /// </summary>
        public WriteableBitmap BitmapTarget { get; private set; }

        public ITrace Trace { get; } = TracerST.Instance;

        /// <summary>
        /// The Image control that we should be writing to. The <see cref="WPFImage.Source"/> proeprty will be overriden during processing.
        /// </summary>
        public WPFImage WriteTo { get; }

        private MediaTypeWrapper InitializedMediaType { get; set; }

        /// <summary>
        /// Always lock before dispatching to the UI thread.
        /// Multiple samples could come in simultaneously and we need to dispatch everything first.
        /// </summary>
        private object WriteLock { get; } = new();

        public void Close()
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                WriteTo.Source = null;
            });
            BitmapTarget = null;
            InitializedMediaType = null;
        }

        /// <summary>
        /// Copies the sample to the <see cref="WriteTo"/> image's Source property using a writeable bitmap.
        /// If the media type changes between samples, create a new writeable bitmap and update the source appropriately.
        /// </summary>
        public void WriteSample(SampleWrapper sample)
        {
            var pSample = sample.Instance;
            var mediaType = sample.MediaType;
            if (Application.Current?.Dispatcher is null)
            {
                // We are exiting.
                return;
            }

            // First ensure that there is a valid target to write the sample data to.
            if (BitmapTarget is null || InitializedMediaType?.FrameSize != mediaType.FrameSize)
            {
                lock (WriteLock)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        System.Drawing.Size size = mediaType.FrameSize;
                        Trace.Info($"Constructing writeable bitmap on GUI thread. [Size: {size.Width}x{size.Height}]");
                        BitmapTarget = new WriteableBitmap(
                        size.Width,
                        size.Height,
                        96, 96,
                        PixelFormats.Bgra32, null
                    );
                        WriteTo.Source = BitmapTarget;
                        InitializedMediaType = mediaType;
                    });
                }
            }

            Trace.Verbose("Locking to render frame sample");
            lock (this)
            {
                IMFMediaBuffer frameBuffer = null;
                IMF2DBuffer frameBuffer2d = null;
                try
                {
                    IntPtr scanlineBuffer = IntPtr.Zero;
                    int lStride = 0;
                    if (pSample != null)
                    {
                        Trace.Verbose($"Found frame sample.");
                        // Get the video frame buffer from the sample.
                        pSample.GetBufferByIndex(0, out frameBuffer).CheckResult();
                        // Helper object to lock the video buffer.
                        // Lock the video buffer. This method returns a pointer to the first scan
                        // line in the image, and the stride in bytes.
                        frameBuffer2d = frameBuffer as IMF2DBuffer;

                        Trace.Verbose("Locking and acquiring frame sample buffer.");
                        frameBuffer2d.Lock2D(out scanlineBuffer, out lStride).CheckResult();
                        var convertImage = UnmanagedImageConvert.GetConversionFunction(mediaType.VideoSubtype);

                        //Put this code in a method that is called from the background thread
                        IntPtr pBackBuffer = IntPtr.Zero;
                        int backBufferStride = 0;
                        int width = 0, height = 0;

                        lock (WriteLock)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {//lock bitmap in ui thread
                                Trace.Verbose("Locking writeable bitmap target.");
                                BitmapTarget.Lock();
                                pBackBuffer = BitmapTarget.BackBuffer;//Make pointer available to background thread
                                backBufferStride = BitmapTarget.BackBufferStride;
                                width = BitmapTarget.PixelWidth;
                                height = BitmapTarget.PixelHeight;
                            });
                        }

                        //Back to the worker thread
                        unsafe
                        {
                            Trace.Verbose("Converting image to BGRA.");
                            try
                            {
                                unsafe
                                {
                                    convertImage(
                                        pBackBuffer,
                                        backBufferStride,
                                        scanlineBuffer,
                                        lStride,
                                        width,
                                        height
                                    );
                                }
                            }
                            catch (Exception e)
                            {
                                Trace.Error(ExceptionMessage.Handled(e, $"Encountered issue writing pixels to bitmap."));
                            }
                        }

                        lock (WriteLock)
                        {
                            Application.Current?.Dispatcher?.Invoke(() =>
                            {
                                Trace.Verbose($"Unlocking writeable bitmap target.");
                                //UI thread does post update operations
                                BitmapTarget.AddDirtyRect(new System.Windows.Int32Rect(0, 0, width, height));
                                BitmapTarget.Unlock();
                            });
                        }

                        frameBuffer2d.Unlock2D();
                    }

                    Trace.Verbose($"Returning from sample read method.");
                }
                catch (Exception e)
                {
                    Trace.Error(ExceptionMessage.Handled(e, $"Failed to render bitmap to view."));
                }
                finally
                {
                    COMBase.SafeRelease(frameBuffer);
                    COMBase.SafeRelease(frameBuffer2d);
                }
            }
        }
    }
}