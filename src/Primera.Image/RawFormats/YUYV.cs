namespace Primera.ImageTransform.RawFormats
{
    /// <summary>
    /// A struct that describes a YUYV pixel
    /// </summary>
    public struct YUYV
    {
        public byte Y;
        public byte U;
        public byte Y2;
        public byte V;

        public YUYV(byte y, byte u, byte y2, byte v)
        {
            Y = y;
            U = u;
            Y2 = y2;
            V = v;
        }
    }
}