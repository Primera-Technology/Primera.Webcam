using System;

namespace CameraCapture.WPF.VideoCapture
{
    public record VideoFormatGUID
    {
        public VideoFormatGUID(Guid FormatGuid, FrameConversionDelegate cvt)
        {
            SubType = FormatGuid;
            VideoConvertFunction = cvt;
        }

        public FrameConversionDelegate VideoConvertFunction { get; }
        public Guid SubType { get; }
    }
}