using System;
using System.Drawing;
using System.Drawing.Imaging;

using CameraCapture.WPF.VideoCapture;

using MediaFoundation;
using MediaFoundation.Misc;

using Primera.Common.Logging;

namespace Primera.Webcam.Device
{
    /// <summary>
    /// A Media Foundation sample is a rendered image stored in memory.
    /// The format is variable, and represented by <see cref="MediaType"/>, though this class handles the parsing and transformation from that format into BGRA32.
    /// If other conversions are desired, custom implementations must be written.
    /// <para/>
    /// To access the source COM object, see <see cref="Instance"/>.
    /// </summary>
    public class SampleWrapper : IDisposable
    {
        private bool _isDisposed = false;

        /// <summary>
        /// Wrap a media foundation sample with its contextual information
        /// </summary>
        internal SampleWrapper(IMFSample instance, int frameNumber, MediaTypeWrapper mediaType)
        {
            Instance = instance;
            FrameNumber = frameNumber;
            MediaType = mediaType;
        }

        /// <summary>
        /// How many frames were rendered prior to this one?
        /// </summary>
        public int FrameNumber { get; }

        /// <summary>
        /// The Media Foundation COM object to be manipulated
        /// </summary>
        public IMFSample Instance { get; }

        /// <summary>
        /// The media type that this sample is created with.
        /// </summary>
        public MediaTypeWrapper MediaType { get; }

        public ITrace Trace { get; } = TracerST.Instance;

        /// <summary>
        /// To prevent multiple readers of the sample buffer, lock access to it to maintain thread safety.
        /// </summary>
        private object SampleLock { get; } = new();

        public void Dispose()
        {
            _isDisposed = true;
            COMBase.SafeRelease(Instance);
        }

        /// <summary>
        /// Copy the contents of this buffer into a <see cref="Bitmap"/> object for more flexible, but lower performance, processing.
        /// </summary>
        /// <returns>A bitmap copy of this buffer</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        public Bitmap GetBitmap()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(SampleWrapper));

            Size size = MediaType.FrameSize;
            Bitmap bitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppRgb);
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, size.Width, size.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppRgb);
            try
            {
                CopySampleBufferMemory(data.Scan0, data.Stride, data.Width, data.Height);
                return bitmap;
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        /// <summary>
        /// Copy this samples frame buffer to a target memory location in BGRA32 format.
        /// Holds a synchronizing lock for the duration of the copy operation for thread safety.
        /// <para/>
        /// Automatically detects the proper converter function to use when copying to the BGRA32 format.
        /// </summary>
        /// <param name="destinationLocation">A pointer to the first scanline of the destination buffer</param>
        /// <param name="destinationStride">The stride of the destination data</param>
        /// <param name="destinationWidth">The total width of a row of destination data</param>
        /// <param name="destinationHeight">The total height of a column of destination data.</param>
        /// <returns>True if the buffer was copied successfully, false otherwise.</returns>
        public bool CopySampleBufferMemory(
            IntPtr destinationLocation,
            int destinationStride,
            int destinationWidth,
            int destinationHeight)
        {
            Trace.Verbose("Locking to copy frame sample");
            lock (SampleLock)
            {
                IMFMediaBuffer frameBuffer = null;
                IMF2DBuffer frameBuffer2d = null;
                try
                {
                    IntPtr scanlineBuffer = IntPtr.Zero;
                    Trace.Verbose($"Found frame sample.");
                    // Get the video frame buffer from the sample.
                    Instance.GetBufferByIndex(0, out frameBuffer).CheckResult();
                    // Helper object to lock the video buffer.
                    // Lock the video buffer. This method returns a pointer to the first scan
                    // line in the image, and the stride in bytes.
                    frameBuffer2d = frameBuffer as IMF2DBuffer;

                    Trace.Verbose("Locking and acquiring frame sample buffer.");
                    frameBuffer2d.Lock2D(out scanlineBuffer, out int lStride).CheckResult();
                    var convertImage = UnmanagedImageConvert.GetConversionFunction(MediaType.VideoSubtype);

                    //Back to the worker thread
                    unsafe
                    {
                        Trace.Verbose("Converting image to BGRA.");
                        try
                        {
                            unsafe
                            {
                                convertImage(
                                    destinationLocation,
                                    destinationStride,
                                    scanlineBuffer,
                                    lStride,
                                    destinationWidth,
                                    destinationHeight
                                );
                            }
                        }
                        catch (Exception e)
                        {
                            Trace.Error(ExceptionMessage.Handled(e, $"Encountered issue writing pixels to bitmap."));
                        }
                    }

                    frameBuffer2d.Unlock2D();

                    Trace.Verbose($"Returning from sample read method.");
                    return true;
                }
                catch (Exception e)
                {
                    Trace.Error(ExceptionMessage.Handled(e, $"Failed to render bitmap to view."));
                    return false;
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