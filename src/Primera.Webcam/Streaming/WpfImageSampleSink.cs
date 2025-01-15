using System;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
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
    /// It is crucial that window events are registered with <see cref="RegisterWindowEvents(Window)"/> in order to prevent catastrophic directX deadlocks.
    /// </summary>
    public class WpfImageSampleSink : ISampleSink
    {
        /// <summary>
        /// Always lock before dispatching to the UI thread.
        /// Multiple samples could come in simultaneously and we need to dispatch everything first.
        /// </summary>
        private SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public WpfImageSampleSink(WPFImage writeTo, ITrace trace)
        {
            WriteTo = writeTo;
            Trace = trace;
        }

        #region Window events

        private bool IOwnSemaphore { get; set; } = false;

        /// <summary>
        /// Register the window events for the main window.
        /// <see cref="RegisterWindowEvents(Window)"/> for why this is important.
        /// </summary>
        public void RegisterMainWindowEvents()
        {
            RegisterWindowEvents(Application.Current.MainWindow);
        }

        /// <summary>
        /// Register the window events for the given window.
        /// This will prevent DirectX deadlocks and is crucially important for the correct operation of the video sink.
        /// </summary>
        public void RegisterWindowEvents(Window window)
        {
            var helper = new WindowInteropHelper(window);
            var source = HwndSource.FromHwnd(helper.Handle);
            source?.AddHook(WndProc);
        }

        /// <summary>
        /// If the semaphore is not owned, attempt to get it.
        /// If we already have it, we don't need to get it again.
        /// <para/>
        /// For use synchronizing only the window events, since the semaphore should be owned until the window event is complete,
        /// and since we do not know when the semaphore will be successfully acquired, this method must be idempotent.
        /// </summary>
        private bool ReentrantGetSemaphore()
        {
            if (!IOwnSemaphore)
            {
                IOwnSemaphore = _semaphore.Wait(0);
            }

            return IOwnSemaphore;
        }

        private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
        {
            // Prevent the window from being moved or resized if a sample is being written to
            switch (msg)
            {
                case 0x0216: // WM_MOVING
                case 0x0214: // WM_SIZING
                    /* If we are trying to move or resize the window, we need to control the frame semaphore to prevent deadlocks.
                     * In the case that we cannot get the semaphore, we will not allow the window to be moved or resized.
                     */
                    if (!ReentrantGetSemaphore())
                    {
                        handled = true;
                    }
                    break;

                case 0x0232: // WM_EXITSIZEMOVE
                    /* Once the move or resize operation is complete, release the semaphore to begin rendering again */
                    if (IOwnSemaphore)
                    {
                        _semaphore.Release();
                        IOwnSemaphore = false;
                    }
                    break;

                default:
                    break;
            }

            return IntPtr.Zero;
        }

        #endregion Window events

        /// <summary>
        /// If not null, this bitmap target is assignd to the given Image Source
        /// </summary>
        public WriteableBitmap BitmapTarget { get; private set; }

        public ITrace Trace { get; }

        /// <summary>
        /// The Image control that we should be writing to. The <see cref="WPFImage.Source"/> proeprty will be overriden during processing.
        /// </summary>
        public WPFImage WriteTo { get; }

        private MediaTypeWrapper InitializedMediaType { get; set; }

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
            if (Application.Current?.Dispatcher is null)
            {
                // We are exiting.
                return;
            }

            // Try to enter the semaphore without blocking
            if (!_semaphore.Wait(0))
            {
                // If the semaphore is not available, discard the frame
                Trace.Verbose("Discarding frame as another frame is being processed.");
                return;
            }

            try
            {
                var mediaType = sample.MediaType;
                // First ensure that there is a valid target to write the sample data to.
                if (BitmapTarget is null || InitializedMediaType?.FrameSize != mediaType.FrameSize)
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

                Trace.Verbose("Locking to render frame sample");

                if (sample != null)
                {
                    Trace.Verbose($"Found frame sample.");

                    // Put this code in a method that is called from the background thread
                    IntPtr pBackBuffer = IntPtr.Zero;
                    int backBufferStride = 0;
                    int width = 0, height = 0;

                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        Trace.Verbose("Locking writeable bitmap target.");
                        BitmapTarget.Lock();
                        pBackBuffer = BitmapTarget.BackBuffer;  // Make pointer available to background thread
                        backBufferStride = BitmapTarget.BackBufferStride;
                        width = BitmapTarget.PixelWidth;
                        height = BitmapTarget.PixelHeight;
                    }));

                    sample.CopySampleBufferMemory(pBackBuffer, width, height);

                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        Trace.Verbose($"Unlocking writeable bitmap target.");
                        // UI thread does post update operations
                        BitmapTarget.AddDirtyRect(new System.Windows.Int32Rect(0, 0, width, height));
                        BitmapTarget.Unlock();
                        Trace.Verbose($"Writeable bitmap target unlocked.");

                        _semaphore.Release();
                        Trace.Verbose($"Semaphore released.");
                    }));
                }
            }
            catch (Exception e)
            {
                Trace.Error(ExceptionMessage.Handled(e, $"Failed to render bitmap to view."));
            }
        }
    }
}