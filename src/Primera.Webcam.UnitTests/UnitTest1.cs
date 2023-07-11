using System.Drawing;
using System.Drawing.Imaging;

using Emgu.CV;

namespace Primera.Webcam.UnitTests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            var source = new Bitmap(1, 1, PixelFormat.Format24bppRgb);
            source.SetPixel(0, 0, Color.WhiteSmoke);

            var dest = new Bitmap(1, 1, PixelFormat.Format32bppArgb);

            var data = source.LockBits(new Rectangle(0, 0, 1, 1), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
            var destData = dest.LockBits(new Rectangle(0, 0, 1, 1), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);

            var rgbMat = new Mat(1, 1, Emgu.CV.CvEnum.DepthType.Cv8U, 3, data.Scan0, 3);
            var rgbaMat = new Mat(1, 1, Emgu.CV.CvEnum.DepthType.Cv8U, 4, destData.Scan0, 4);

            Emgu.CV.CvInvoke.CvtColor(rgbMat, rgbaMat, Emgu.CV.CvEnum.ColorConversion.Rgb2Rgba);

            source.UnlockBits(data);
            dest.UnlockBits(destData);

            Assert.AreEqual(dest.GetPixel(0, 0), Color.WhiteSmoke);
        }
    }
}