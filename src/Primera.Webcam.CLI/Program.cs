using System.CommandLine;

using CameraCapture.WPF.VideoCapture;

using Optional;

using Primera.Webcam.Device;

namespace Primera.Webcam.CLI
{
    internal class Program
    {
        public static void CaptureImage(string deviceName, string mediaTypeId, string filepath)
        {
            var device = SelectVidcapDevice(deviceName);
            if (device is null) return;

            var maybeReader = GetDefaultSourceReader(device);
            maybeReader.FlatMap(sourceReader =>
            {
                var maybeMedia = SelectMediaType(sourceReader, mediaTypeId);
                return maybeMedia.FlatMap(mediaType =>
                {
                    sourceReader.SetMediaType(mediaType);
                    Console.WriteLine("Reading Sample");
                    sourceReader.ReadSample().MatchSome(sample => sample.Dispose());
                    return sourceReader.ReadSample();
                });
            }).MatchSome(sample =>
            {
                Console.WriteLine("Saving Bitmap");
                var bmp = sample.GetBitmap();
                bmp.Save(filepath);
                sample.Dispose();
            });
        }

        public static IReadOnlyList<CaptureDeviceDTO> GetCaptureDeviceDTOs()
        {
            var devices = CaptureDeviceWrapper.GetAllDevices();

            List<CaptureDeviceDTO> dtos = new();
            foreach (var d in devices)
            {
                var maybeName = d.GetFriendlyName();
                maybeName.MatchSome(name =>
                {
                    var maybeSymb = d.GetSymbolicName();
                    maybeSymb.MatchSome(symb =>
                    {
                        dtos.Add(new CaptureDeviceDTO(name, symb));
                    });
                });
            }

            return dtos;
        }

        public static Optional.Option<SourceReaderWrapper> GetDefaultSourceReader(CaptureDeviceWrapper device)
        {
            return SourceReaderOptionsWrapper.Create()
               .FlatMap(options =>
               {
                   options.DisableReadWriteConverters(true);
                   return device.Activate().FlatMap(mediaSource => mediaSource.CreateSourceReader(options));
               });
        }

        public static Optional.Option<IReadOnlyList<MediaTypeDTO>> GetMediaTypeDTOs(CaptureDeviceWrapper device)
        {
            return GetDefaultSourceReader(device)
                .Map(reader => reader.MediaTypes)
                .Map<IReadOnlyList<MediaTypeDTO>>(mediaTypes => mediaTypes.Select(MediaTypeDTO.Create).ToList());
        }

        public static void ListCaptureDevices()
        {
            var devices = GetCaptureDeviceDTOs();
            foreach (var d in devices)
            {
                Console.WriteLine($"{d.FriendlyName} : {d.SymbolicName}");
            }
        }

        public static void ListMediaTypes(string deviceName)
        {
            var device = SelectVidcapDevice(deviceName);
            if (device is null) return;

            var maybeMediaTypes = GetMediaTypeDTOs(device);
            maybeMediaTypes.MatchSome(mediaTypes =>
            {
                foreach (var m in mediaTypes)
                {
                    Console.WriteLine(m.ID);
                }
            });
        }

        public static Optional.Option<MediaTypeWrapper> SelectMediaType(SourceReaderWrapper device, string mediaTypeId)
        {
            foreach (var m in device.MediaTypes)
            {
                // use the dto to generate the ID for now
                var dto = MediaTypeDTO.Create(m);
                if (Equals(dto.ID, mediaTypeId))
                {
                    return m.Some();
                }
            }
            Console.WriteLine($"Media type was not found for ID: {mediaTypeId}");
            return Optional.Option.None<MediaTypeWrapper>();
        }

        public static CaptureDeviceWrapper? SelectVidcapDevice(string deviceFriendlyName)
        {
            var devices = CaptureDeviceWrapper.GetAllDevices();

            return devices.FirstOrDefault(d =>
            {
                return d.GetFriendlyName().Match(name => Equals(deviceFriendlyName, name), () => false);
            });
        }

        [MTAThread]
        private static int Main(string[] args)
        {
            RootCommand rootCommand = new RootCommand("An application to communciate with Video Capture Devices");

            /*
             * List Video Capture Devices
             */
            var listDevicesCommand = new Command("devices", "List all video capture devices");
            listDevicesCommand.SetHandler(ListCaptureDevices);

            rootCommand.AddCommand(listDevicesCommand);

            /*
             * List Video Types for a capture device
             */
            var deviceNameOption = new System.CommandLine.Option<string>("--deviceName", description: "The friendly name given from a capture device")
            {
                IsRequired = true
            };

            var listMediaTypesCommand = new Command("mediaTypes", "List all media types for a given capture device")
            {
                deviceNameOption
            };

            listMediaTypesCommand.SetHandler(ListMediaTypes, deviceNameOption);

            rootCommand.AddCommand(listMediaTypesCommand);

            /*
             * Capture a still image from a capture device
             */
            var mediaTypeOption = new System.CommandLine.Option<string>("--mediaType", description: "The Media Type ID to select for media playback.");
            var filepathOption = new System.CommandLine.Option<string>("--filepath", description: "The save location for the captured sample");
            var captureCommand = new Command("capture", "Capture a still image from a given capture device and media type")
            {
                deviceNameOption,
                mediaTypeOption,
                filepathOption
            };
            captureCommand.SetHandler(CaptureImage, deviceNameOption, mediaTypeOption, filepathOption);
            rootCommand.AddCommand(captureCommand);

            var result = rootCommand.Invoke(args);
            return result;
        }
    }
}