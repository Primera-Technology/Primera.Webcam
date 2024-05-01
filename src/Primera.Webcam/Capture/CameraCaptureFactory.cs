using System.Linq;

using CameraCapture.WPF.VideoCapture;

using MediaFoundation.Misc;

using Optional;

using Primera.Common.Logging;
using Primera.Webcam.Device;

namespace Primera.Webcam.Capture
{
    public static class CameraCaptureFactory
    {
        public static ITrace Trace => CameraCaptureTracing.Trace;

        public static Option<SourceReaderWrapper> GetDefaultSourceReader(CaptureDeviceWrapper device)
        {
            return SourceReaderOptionsWrapper.Create()
               .FlatMap(options =>
               {
                   options.DisableReadWriteConverters(true);
                   return device.Activate().FlatMap(mediaSource => mediaSource.CreateSourceReader(options));
               });
        }

        public static Option<MediaTypeWrapper> SelectMediaType(SourceReaderWrapper device, MediaTypeSelector mediaTypeSelector)
        {
            var mediaType = device.MediaTypes
                .Where(m => m.FrameSize.Height == mediaTypeSelector.Resolution.PixelHeight && m.FrameSize.Width == mediaTypeSelector.Resolution.PixelWidth)
                .Where(m => m.FrameRate >= mediaTypeSelector.MinimumFramerate)
                .Where(m => Equals(new FourCC(m.VideoSubtype).ToString(), mediaTypeSelector.Encoding.ToString()))
                .OrderByDescending(m => m.FrameRate)
                .ThenByDescending(m => m.FrameSize.Height)
                .FirstOrDefault();

            if (mediaType is null)
            {
                Trace.Error($"Media type not found for resolution: {mediaTypeSelector.Resolution} and encoding: {mediaTypeSelector.Encoding}.");
                return Option.None<MediaTypeWrapper>();
            }
            else
            {
                return mediaType.Some();
            }
        }

        public static Option<CaptureDeviceWrapper> SelectVidcapDevice(string deviceFriendlyName)
        {
            var devices = CaptureDeviceWrapper.GetAllDevices();

            var device = devices.FirstOrDefault(d =>
            {
                return d.GetFriendlyName().Match(name => Equals(deviceFriendlyName, name), () => false);
            });

            if (device is null)
            {
                Trace.Error($"Device was not found for friendly name: {deviceFriendlyName}. Get a list of all devices using the \"devices\" command.");
                return Option.None<CaptureDeviceWrapper>();
            }
            else
            {
                Trace.Error($"Device found for name: {deviceFriendlyName}");
                return device.Some();
            }
        }
    }
}