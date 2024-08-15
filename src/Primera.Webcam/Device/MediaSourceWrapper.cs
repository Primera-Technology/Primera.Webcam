using MediaFoundation;
using MediaFoundation.Misc;
using MediaFoundation.ReadWrite;

using Optional;

namespace Primera.Webcam.Device
{
    /// <summary>
    /// A Media Source is an activated device that contains interfaces and actions that interact with that device.
    /// The primary use of a Media Source in this library is <see cref="CreateSourceReader(SourceReaderOptionsWrapper)"/>.
    /// </summary>
    public class MediaSourceWrapper
    {
        internal MediaSourceWrapper(IMFMediaSource instance, CaptureDeviceWrapper parent)
        {
            Instance = instance;
            Parent = parent;
        }

        /// <summary>
        /// The Media Foundation COM object to be manipulated
        /// </summary>
        public IMFMediaSource Instance { get; }

        public CaptureDeviceWrapper Parent { get; }

        /// <summary>
        /// This media source can be consumed, but not until an object is created to actually read from it.
        /// That's the, you guessed it, "Source Reader".
        /// </summary>
        public Option<SourceReaderWrapper> CreateSourceReader(SourceReaderOptionsWrapper options)
        {
            HResult result = MFExtern.MFCreateSourceReaderFromMediaSource(Instance, options.Instance, out IMFSourceReader sourceReader);
            if (COMBase.Failed(result))
            {
                CameraCaptureTracing.Trace.Warning($"Failed to create source reader from media source. {result}");
                return Option.None<SourceReaderWrapper>();
            }

            return new SourceReaderWrapper(sourceReader, this).Some();
        }
    }
}