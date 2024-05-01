using System;
using System.Collections.Generic;
using System.Threading;

using CameraCapture.WPF.VideoCapture;

using MediaFoundation;
using MediaFoundation.Misc;
using MediaFoundation.ReadWrite;

using Optional;

using Primera.Common.Logging;

namespace Primera.Webcam.Device
{
    /// <summary>
    /// <para/>
    /// For more details on the Source Reader, see: https://learn.microsoft.com/en-us/windows/win32/medfound/source-reader
    /// </summary>
    public class SourceReaderWrapper : IDisposable
    {
        internal SourceReaderWrapper(IMFSourceReader instance)
        {
            Instance = instance;
            LazyMediaTypes = new(GetMediaTypes);
        }

        public SynchronizationContext CreationContext { get; }

        /// <summary>
        /// The Media Foundation COM object to be manipulated
        /// </summary>
        public IMFSourceReader Instance { get; }

        public IReadOnlyList<MediaTypeWrapper> MediaTypes => LazyMediaTypes.Value;

        public int NextFrameNumber { get; private set; }

        public MediaTypeWrapper SelectedMediaType { get; private set; }

        public ITrace Trace => CameraCaptureTracing.Trace;

        private Lazy<List<MediaTypeWrapper>> LazyMediaTypes { get; }

        public void Dispose()
        {
            COMBase.SafeRelease(Instance);
        }

        public List<MediaTypeWrapper> GetMediaTypes()
        {
            List<MediaTypeWrapper> typeCollection = new();
            for (int i = 0; ; i++)
            {
                HResult hr = Instance.GetNativeMediaType((int)MF_SOURCE_READER.FirstVideoStream, i, out IMFMediaType nextMediaType);
                if (COMBase.Failed(hr))
                {
                    break;
                }

                var wrapped = MediaTypeWrapper.Create(nextMediaType);
                typeCollection.Add(wrapped);
            }

            return typeCollection;
        }

        public Option<SampleWrapper> ReadSample()
        {
            if (SelectedMediaType is null)
            {
                throw new InvalidOperationException("Must set media type prior to reading any samples.");
            }

            lock (this)
            {
                Trace.Info($"Reading synchronous sample {NextFrameNumber}");
                HResult result = Instance.ReadSample(
                    (int)MF_SOURCE_READER.FirstVideoStream,
                    0,
                    out int streamIndex,
                    out MF_SOURCE_READER_FLAG flags,
                    out long timestamp,
                    out IMFSample sample
                );
                Trace.Info($"Reading synchronous sample read. [HR: {result}]");

                if (COMBase.Failed(result))
                {
                    Trace.Error("Could not read synchronous sample.");
                    return Option.None<SampleWrapper>();
                }

                var wrapped = new SampleWrapper(sample, NextFrameNumber++, SelectedMediaType);
                return wrapped.Some();
            }
        }

        public void SetMediaType(MediaTypeWrapper mediaType)
        {
            HResult result = Instance.SetCurrentMediaType(SourceReaderFirstStream.FirstVideoStream, mediaType.Instance).CheckResult();

            SelectedMediaType = mediaType;
        }
    }
}