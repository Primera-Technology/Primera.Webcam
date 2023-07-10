using Primera.Webcam.Device;

namespace Primera.Webcam.Streaming
{
    public class StillImageSampleSink : ISampleSink
    {
        public string CapturePath { get; set; }

        public bool CaptureNextFrame { get; set; }

        public void WriteSample(SampleWrapper sample)
        {
            // Only process when its time to write the sample to file
            if (!CaptureNextFrame) return; 
            
            CaptureNextFrame = false;

            var bitmap = sample.GetBitmap();
            bitmap.Save(CapturePath);
        }
    }
}