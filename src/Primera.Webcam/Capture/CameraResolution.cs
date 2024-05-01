namespace Primera.Webcam.Capture
{
    public enum AspectRatios
    {
        FourByThree,
        SixteenByNine,
        Square,
    }

    public class CameraResolution
    {
        public CameraResolution(int pixelHeight, AspectRatios ratio)
        {
            PixelHeight = pixelHeight;
            Ratio = ratio;
            PixelWidth = GetWidthFromRatio(PixelHeight, Ratio);
        }

        public int PixelHeight { get; }

        public int PixelWidth { get; }

        public AspectRatios Ratio { get; }

        /// <summary>
        /// Creates a camera resolution with given height at 4/3 aspect ratio
        /// </summary>
        public static CameraResolution StandardAspect(int height)
        {
            return new CameraResolution(height, AspectRatios.FourByThree);
        }

        public double SizeRatio(CameraResolution other)
        {
            return (double)other.PixelWidth / PixelWidth;
        }

        public override string ToString()
        {
            return $"{PixelWidth}x{PixelHeight}";
        }

        private static int GetWidthFromRatio(int height, AspectRatios ratio)
        {
            return ratio switch
            {
                AspectRatios.FourByThree => height * 4 / 3,
                AspectRatios.SixteenByNine => height * 16 / 9,
                AspectRatios.Square => height,
                _ => height,
            };
        }
    }
}