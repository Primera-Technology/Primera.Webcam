using System.Runtime.InteropServices;

namespace Primera.Image.RawFormats
{

    /// <summary>
    /// 24-bit RGB representation, with 8-bits each for R, G, and B, with no Alpha channel
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct BGR24
    {
        public byte B;
        public byte G;
        public byte R;
    }
}