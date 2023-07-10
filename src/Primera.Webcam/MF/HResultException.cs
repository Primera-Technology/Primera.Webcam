using MediaFoundation;

using System;

namespace CameraCapture.WPF.VideoCapture
{
    public class HResultException : Exception
    {
        public HResultException(HResult result) : base($"HRESULT given was failing: {result}")
        {
            Result = result;
        }

        public HResult Result { get; }
    }
}