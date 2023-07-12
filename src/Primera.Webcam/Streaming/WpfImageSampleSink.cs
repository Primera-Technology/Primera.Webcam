using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using CameraCapture.WPF.VideoCapture;

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
                try
                {
                    if (sample != null)
                    {
                        Trace.Verbose($"Found frame sample.");

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
                                pBackBuffer = BitmapTarget.BackBuffer;  //Make pointer available to background thread
                                backBufferStride = BitmapTarget.BackBufferStride;
                                width = BitmapTarget.PixelWidth;
                                height = BitmapTarget.PixelHeight;
                            });
                        }

                        sample.CopySampleBufferMemory(pBackBuffer, width, height);

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
                    }

                    Trace.Verbose($"Returning from sample read method.");
                }
                catch (Exception e)
                {
                    Trace.Error(ExceptionMessage.Handled(e, $"Failed to render bitmap to view."));
                }
            }
        }
    }
}