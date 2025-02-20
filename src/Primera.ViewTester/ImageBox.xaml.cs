﻿using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

using DirectShowLib;

using Optional;
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

        public SynchronizedObject<CaptureDeviceDSWrapper> Filter { get; private set; }

        public WpfImageSampleSink Sink { get; private set; }

        public MTAThreadSynchronizer Synchronizer { get; private set; }

        public void Checkbox_Checked(object sender, RoutedEventArgs e)
        {
            if (Filter is null) return;

            Filter.Post(f => f.ExposureFlagCurrent = CameraControlFlags.Auto);
        }

        public void Checkbox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (Filter is null) return;

            Filter.Post(f => f.ExposureFlagCurrent = CameraControlFlags.Manual);
        }

        public void DownButton_Click(object sender, RoutedEventArgs e)
        {
            if (Filter is null) return;

            Filter.Post(f => f.ExposureCurrent--);
        }

        public void UpButton_Click(object sender, RoutedEventArgs e)
        {
            if (Filter is null) return;

            Filter.Post(f => f.ExposureCurrent++);
        }

        private void ImageBox_Loaded(object sender, RoutedEventArgs e)
        {
            var devices = CaptureDeviceWrapper.GetAllDevices();
            var device = devices.FirstOrDefault();
            if (device is null) return;

            var maybeName = device.GetFriendlyName();
            if (!maybeName.HasValue) return;
            string name = maybeName.ValueOrFailure();

            Synchronizer = new MTAThreadSynchronizer();

            var cameraStream = SynchronizedObject<CameraCaptureStream>.CreateOption(Synchronizer, () =>
            {
                var maybeStream = CameraCaptureStream.OpenCamera(name, null);
                if (!maybeStream.HasValue) return Option.None<CameraCaptureStream>();
                return maybeStream.WithoutException();
            });
            var stream = cameraStream.ValueOrFailure();

            var syncFilter = SynchronizedObject<CaptureDeviceDSWrapper>.CreateOption(Synchronizer, () =>
            {
                var filters = CaptureDeviceDSWrapper.EnumerateVideoDevices();

                var matching = filters.Where(f => f.FriendlyName == name).FirstOrDefault();
                if (matching is null)
                {
                    return Option.None<CaptureDeviceDSWrapper>();
                }
                else
                {
                    return matching.Some();
                }
            });

            Filter = syncFilter.ValueOrFailure();

            Sink = new WpfImageSampleSink(image, TracerST.Instance);
            Sink.RegisterMainWindowEvents();

            stream.Obj.FrameAvailable += Stream_FrameAvailable;
        }

        private void Stream_FrameAvailable(object? sender, SampleWrapper e)
        {
            Sink?.WriteSample(e);
        }
    }
}