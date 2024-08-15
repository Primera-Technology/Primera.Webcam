using System.Windows;
using System.Windows.Controls;

using DirectShowLib;

using Optional.Unsafe;

using Primera.Common.Logging;
using Primera.Webcam.Capture;
using Primera.Webcam.Device;
using Primera.Webcam.DirectShow;
using Primera.Webcam.Streaming;

namespace Primera.ViewTester
{
    /// <summary>
    /// Interaction logic for ImageBox.xaml
    /// </summary>
    public partial class ImageBox : UserControl
    {
        public ImageBox()
        {
            InitializeComponent();
            Loaded += ImageBox_Loaded;
        }

        public CaptureDeviceDSWrapper? Filter { get; private set; }

        public WpfImageSampleSink Sink { get; private set; }

        public void Checkbox_Checked(object sender, RoutedEventArgs e)
        {
            if (Filter is null) return;

            Filter.ExposureFlagCurrent = CameraControlFlags.Auto;
        }

        public void Checkbox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (Filter is null) return;

            Filter.ExposureFlagCurrent = CameraControlFlags.Manual;
        }

        public void DownButton_Click(object sender, RoutedEventArgs e)
        {
            if (Filter is null) return;

            Filter.ExposureCurrent--;
        }

        public void UpButton_Click(object sender, RoutedEventArgs e)
        {
            if (Filter is null) return;

            Filter.ExposureCurrent++;
        }

        private void ImageBox_Loaded(object sender, RoutedEventArgs e)
        {
            var devices = CaptureDeviceWrapper.GetAllDevices();
            var device = devices.FirstOrDefault();
            if (device is null) return;

            var maybeName = device.GetFriendlyName();
            if (!maybeName.HasValue) return;
            string name = maybeName.ValueOrFailure();

            var maybeStream = CameraCaptureStream.OpenCamera(name, null);
            if (!maybeStream.HasValue) return;
            var stream = maybeStream.ValueOrFailure();

            var filters = CaptureDeviceDSWrapper.GetAllDevices();
            Filter = CaptureDeviceDSWrapper.GetAllDevices().FirstOrDefault();

            Sink = new WpfImageSampleSink(image, new NullTracer());
            stream.FrameAvailable += Stream_FrameAvailable;
        }

        private void Stream_FrameAvailable(object? sender, SampleWrapper e)
        {
            Sink?.WriteSample(e);
        }
    }
}