using CameraCapture.WPF.VideoCapture;

using MediaFoundation.Misc;

namespace Primera.Webcam.CLI
{
    public record MediaTypeDTO
    {
        public MediaTypeDTO(string fourCC, int pixelWidth, int pixelHeight, double frameRate)
        {
            FourCC = fourCC;
            PixelWidth = pixelWidth;
            PixelHeight = pixelHeight;
            FrameRate = frameRate;
            ID = $"{FourCC}:{PixelWidth}x{PixelHeight}@{FrameRate}";
        }

        public string ID { get; }

        public string FourCC { get; }

        public int PixelWidth { get; }

        public int PixelHeight { get; }

        public double FrameRate { get; }

        internal static MediaTypeDTO Create(MediaTypeWrapper m)
        {
            var fourcc = new FourCC(m.VideoSubtype);
            int width = m.FrameSize.Width;
            int height = m.FrameSize.Height;
            return new MediaTypeDTO(fourcc.ToString(), width, height, m.FrameRate);
        }
    }
}