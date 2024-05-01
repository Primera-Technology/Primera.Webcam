using System.Collections.Generic;
using System;

namespace Primera.Webcam.Capture
{
    public class MediaTypeSelector : IEquatable<MediaTypeSelector>
    {
        public MediaTypeSelector()
        {
        }

        public VideoEncoding Encoding { get; set; } = VideoEncoding.YUY2;

        public int MinimumFramerate { get; set; } = 0;

        public CameraResolution Resolution { get; set; } = CameraResolution.StandardAspect(1944);

        public static bool operator !=(MediaTypeSelector left, MediaTypeSelector right)
        {
            return !(left == right);
        }

        public static bool operator ==(MediaTypeSelector left, MediaTypeSelector right)
        {
            return EqualityComparer<MediaTypeSelector>.Default.Equals(left, right);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as MediaTypeSelector);
        }

        public bool Equals(MediaTypeSelector other)
        {
            return other is not null &&
                   Encoding == other.Encoding &&
                   MinimumFramerate == other.MinimumFramerate &&
                   EqualityComparer<CameraResolution>.Default.Equals(Resolution, other.Resolution);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Encoding, MinimumFramerate, Resolution);
        }
    }
}