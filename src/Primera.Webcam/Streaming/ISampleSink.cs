using Primera.Webcam.Device;

namespace Primera.Webcam.Streaming
{
    /// <summary>
    /// GUI frameworks have specific needs for converting a sample into
    /// </summary>
    public interface ISampleSink
    {
        /// <summary>
        /// Given a media foundation, extract the frame data and copy it to a new frame for viewing, processing, etc.
        /// </summary>
        /// <param name="pSample">The frame data to process</param>
        void WriteSample(SampleWrapper pSample);
    }
}