using MediaFoundation;
using MediaFoundation.Misc;

namespace CameraCapture.WPF.VideoCapture
{
    public static class HResultHelpers
    {
        /// <summary>
        /// Throws an exception for failing results or returns the successful result
        /// </summary>
        public static HResult CheckResult(this HResult result)
        {
            if (COMBase.Failed(result))
            {
                throw new HResultException(result);
            }

            return result;
        }
    }
}