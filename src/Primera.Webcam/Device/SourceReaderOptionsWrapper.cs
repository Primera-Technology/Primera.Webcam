using CameraCapture.WPF.VideoCapture;

using MediaFoundation;
using MediaFoundation.Misc;
using MediaFoundation.ReadWrite;

using Optional;

using Primera.Common.Logging;

namespace Primera.Webcam.Device
{
    public class SourceReaderOptionsWrapper
    {
        private SourceReaderOptionsWrapper(IMFAttributes instance)
        {
            Instance = instance;
        }

        public IMFAttributes Instance { get; }

        public ITrace Trace => TracerST.Instance;

        public static Option<SourceReaderOptionsWrapper> Create()
        {
            HResult result = MFExtern.MFCreateAttributes(out IMFAttributes sourceReaderOptions, 2);
            if (COMBase.Failed(result)) return Option.None<SourceReaderOptionsWrapper>();

            return new SourceReaderOptionsWrapper(sourceReaderOptions).Some();
        }

        public void DisableReadWriteConverters(bool value)
        {
            Trace.Verbose("Disabling read/write converters.");
            Instance.SetUINT32(MFAttributesClsid.MF_READWRITE_DISABLE_CONVERTERS, value ? 1 : 0).CheckResult();
        }

        public void SetAsyncCallback(IMFSourceReaderCallback readerCallback)
        {
            Trace.Verbose("Attaching sample reader callback.");
            Instance.SetUnknown(MFAttributesClsid.MF_SOURCE_READER_ASYNC_CALLBACK, readerCallback).CheckResult();
        }
    }
}