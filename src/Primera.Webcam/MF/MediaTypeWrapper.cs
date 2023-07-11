using System;
using System.Drawing;

using MediaFoundation;

namespace CameraCapture.WPF.VideoCapture
{
    public record MediaTypeWrapper
    {
        private MediaTypeWrapper(IMFMediaType instance)
        {
            Instance = instance;
        }

        public IMFMediaType Instance { get; }

        public Size FrameSize { get; init; }

        public Guid VideoSubtype { get; init; }

        public double FrameRate { get; private set; }

        public static MediaTypeWrapper Create(IMFMediaType instance)
        {
            instance.GetGUID(MFAttributesClsid.MF_MT_SUBTYPE, out Guid subType).CheckResult();
            MFExtern.MFGetAttributeSize(instance, MFAttributesClsid.MF_MT_FRAME_SIZE, out int width, out int height).CheckResult();
            MFExtern.MFGetAttributeRatio(instance, MFAttributesClsid.MF_MT_FRAME_RATE, out int numerator, out int denominator);
            double frameRate = (double)numerator / denominator;

            return new MediaTypeWrapper(instance)
            {
                FrameSize = new Size(width, height),
                VideoSubtype = subType,
                FrameRate = frameRate
            };
        }
    }
}