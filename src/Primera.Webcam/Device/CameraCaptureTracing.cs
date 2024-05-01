using Primera.Common.Logging;

namespace Primera.Webcam.Device
{
    public static class CameraCaptureTracing
    {
        internal static ITrace Trace { get; set; } = new NullTracer();

        public static void RegisterTrace(ITrace trace)
        {
            Trace = trace;
        }
    }
}