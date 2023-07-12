using System;

using Emgu.CV;
using Emgu.CV.CvEnum;

using MediaFoundation;
using MediaFoundation.Misc;

namespace CameraCapture.WPF.VideoCapture
{
    public static class UnmanagedImageConvert
    {
        public static void ColorToBGRA(Guid videoType, IntPtr destinationMemory, IntPtr sourceMemory, int pixelWidth, int pixelHeight)
        {
            var fourCC = new FourCC(videoType);
            if (fourCC.ToString() == "32")
            {
                MediaFoundation.MFExtern.MFCopyImage(destinationMemory, pixelWidth * 4, sourceMemory, pixelWidth * 4, pixelWidth * 4, pixelHeight);
            }
            ColorConversion conversion = fourCC.ToString() switch
            {
                "YUY2" => ColorConversion.Yuv2BgraYuy2,
                "NV12" => ColorConversion.Yuv2BgraNv12,
                "24BG" => ColorConversion.Bgr2Bgra,
                _ => throw new ArgumentOutOfRangeException("Image Format was not supported for color conversion.")
            };

            var input = GetMatForFormattedMemory(fourCC, pixelWidth, pixelHeight, sourceMemory);
            var output = new Mat(pixelHeight, pixelWidth, DepthType.Cv8U, 4, destinationMemory, pixelWidth * 4);

            unsafe
            {
                Emgu.CV.CvInvoke.CvtColor(input, output, conversion);
            }
        }

        public static Mat GetMatForFormattedMemory(FourCC videoType, int pixelWidth, int pixelHeight, IntPtr memory)
        {
            string fourccString = videoType.ToString();

            var mat = fourccString switch
            {
                "YUY2" => new Mat(pixelHeight, pixelWidth, DepthType.Cv8U, 2, memory, pixelWidth * 2),

                // NV12 is complicated. There is one block with only luma (Y) values, and a second block of interleaved (U,V) tuples
                // This means, in the memory block there are 1.5x pixel height in rows and 1x pixel width in columns at channel count of 1
                "NV12" => new Mat(pixelHeight + (pixelHeight >> 1), pixelWidth, DepthType.Cv8U, 1, memory, pixelWidth),

                // 24-bit BGR
                "24BG" => new Mat(pixelHeight, pixelWidth, DepthType.Cv8U, 3, memory, pixelWidth * 3),

                // 32-bit BGRA
                "BGRA" => new Mat(pixelHeight, pixelWidth, DepthType.Cv8U, 4, memory, pixelWidth * 4),

                _ => throw new ArgumentOutOfRangeException("No Mat definition found for this image format"),
            };

            return mat;
        }
    }
}