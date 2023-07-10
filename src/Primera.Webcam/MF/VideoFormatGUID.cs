using System;

namespace CameraCapture.WPF.VideoCapture
{
    public record VideoFormatGUID
    {
        public VideoFormatGUID(Guid FormatGuid, VideoConversionDelegate cvt)
        {
            SubType = FormatGuid;
            VideoConvertFunction = cvt;
        }

        public VideoConversionDelegate VideoConvertFunction { get; }
        public Guid SubType { get; }
    }
}