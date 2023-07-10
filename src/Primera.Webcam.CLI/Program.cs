using System.CommandLine;

using CameraCapture.WPF.VideoCapture;

using Optional;

using Primera.Webcam.Device;

namespace Primera.Webcam.CLI
{
    internal class Program
    {
        [MTAThread]
        public static int Main(string[] args)
        {
            RootCommand rootCommand = new RootCommand("An application to communciate with Video Capture Devices");

            var outputFileOption = new System.CommandLine.Option<bool>("--json", "Structure output data in .json format.")
            {
                Arity = ArgumentArity.Zero
            };

            /*
             * List Video Capture Devices
             */
            var listDevicesCommand = new Command("devices",
                "List all video capture devices available on the system. " +
                "Each video capture device has both a friendly name and a symbolic name. " +
                "The friendly name is familiar to the user and is the information displayed by Windows when enumerating the device. " +
                "The symbolic name contains information about the underlying hardware connecting the webcam, e.g. the Vendor and Product ID (VIDPID). " +
                Environment.NewLine + Environment.NewLine +
                "No parsing is performed on the symbolic name to ensure maximum compatibility." +
                Environment.NewLine + Environment.NewLine +
                "After receiving the device name, continue to acquire available media sources through the \"mediaTypes\" command before attempting to capture an image.");
            listDevicesCommand.SetHandler(ListCaptureDevices);

            rootCommand.AddCommand(listDevicesCommand);

            /*
             * List Video Types for a capture device
             */
            var deviceNameOption = new System.CommandLine.Option<string>("--deviceName", description: "The friendly name given from a capture device. Acquired from the \"devices\" command.")
            {
                IsRequired = true
            };

            var listMediaTypesCommand = new Command("mediaTypes",
                "List all media types for a given capture device. " +
                "Each media source has an encoding and an image Resolution. " +
                "The encodings are referenced with their Four CC code - details about which can be found online. " +
                "The codec selection is important due to compression amount and type. " +
                Environment.NewLine + Environment.NewLine +
                "After identifying the appropriate media source for some device, capture an image using the \"capture\" command.")
            {
                deviceNameOption
            };

            listMediaTypesCommand.SetHandler(ListMediaTypes, deviceNameOption);

            rootCommand.AddCommand(listMediaTypesCommand);

            /*
             * Capture a still image from a capture device
             */
            var mediaTypeOption = new System.CommandLine.Option<string>("--mediaType", description: "The Media Source ID to select for media playback, acquired through the \"mediaType\" command.");
            var filepathOption = new System.CommandLine.Option<string>("--filepath",
                description: "A save location for the captured sample. " +
                "This filepath must point to a file location within an already existing directory."
            );
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

        private static void CaptureImage(string deviceName, string mediaTypeId, string filepath)
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
                    Console.Error.WriteLine("Reading Sample");
                    sourceReader.ReadSample().MatchSome(sample => sample.Dispose());
                    return sourceReader.ReadSample();
                });
            }).MatchSome(sample =>
            {
                Console.Error.WriteLine("Saving Bitmap");
                var bmp = sample.GetBitmap();
                bmp.Save(filepath);
                sample.Dispose();
            });
        }

        private static IReadOnlyList<CaptureDeviceDTO> GetCaptureDeviceDTOs()
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

        private static Optional.Option<SourceReaderWrapper> GetDefaultSourceReader(CaptureDeviceWrapper device)
        {
            return SourceReaderOptionsWrapper.Create()
               .FlatMap(options =>
               {
                   options.DisableReadWriteConverters(true);
                   return device.Activate().FlatMap(mediaSource => mediaSource.CreateSourceReader(options));
               });
        }

        private static Optional.Option<IReadOnlyList<MediaTypeDTO>> GetMediaTypeDTOs(CaptureDeviceWrapper device)
        {
            return GetDefaultSourceReader(device)
                .Map(reader => reader.MediaTypes)
                .Map<IReadOnlyList<MediaTypeDTO>>(mediaTypes => mediaTypes.Select(MediaTypeDTO.Create).ToList());
        }

        private static void ListCaptureDevices()
        {
            var devices = GetCaptureDeviceDTOs();
            foreach (var d in devices)
            {
                Console.WriteLine($"{d.FriendlyName} : {d.SymbolicName}");
            }
        }

        private static void ListMediaTypes(string deviceName)
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

        private static Optional.Option<MediaTypeWrapper> SelectMediaType(SourceReaderWrapper device, string mediaTypeId)
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

            Console.Error.WriteLine($"Media type was not found for ID: {mediaTypeId}. Get a list of all media types and their IDs using the \"mediaType\" command.");
            return Optional.Option.None<MediaTypeWrapper>();
        }

        private static CaptureDeviceWrapper? SelectVidcapDevice(string deviceFriendlyName)
        {
            var devices = CaptureDeviceWrapper.GetAllDevices();

            var device = devices.FirstOrDefault(d =>
            {
                return d.GetFriendlyName().Match(name => Equals(deviceFriendlyName, name), () => false);
            });

            if (devices is null)
            {
                Console.Error.WriteLine($"Device was not found for friendly name: {deviceFriendlyName}. Get a list of all devices using the \"devices\" command.");
            }

            return device;
        }
    }
}