using System.Runtime.InteropServices;

namespace Primera.ImageTransform.RawFormats
{
    /// <summary>
    /// 32-bit representation of RGB color with 8-bits each for R, G, B, and Alpha
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct BGRA32
    {
        public byte B;
        public byte G;
        public byte R;
        public byte A;
    }
}