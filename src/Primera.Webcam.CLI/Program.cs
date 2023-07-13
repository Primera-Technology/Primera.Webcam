using System.CommandLine;
using System.Diagnostics;

using CameraCapture.WPF.VideoCapture;

using Newtonsoft.Json;

using Optional;

using Primera.Common.Logging;
using Primera.FileSystem;
using Primera.Webcam.Device;

namespace Primera.Webcam.CLI
{
    public enum ExitCodes
    {
        None = 0,
        UnexpectedError,
        DeviceNotFound,
        MediaTypeNotFound
    }

    public class Program
    {
        public static string? OutputFile { get; set; }

        /// <summary>
        /// Write to this output text prior to exiting command for final output text.
        /// </summary>
        public static string OutputText { get; set; }

        public static bool UseJson { get; set; }

        [MTAThread]
        public static void Main(string[] args)
        {
            ITrace trace = TracerST.Instance;
            var source = new TraceSource("mfcapture")
            {
                Switch = new SourceSwitch("traceSwitch")
                {
                    Level = SourceLevels.Verbose
                },
            };
            source.Listeners.Add(new ArchivableFileListener(PrimeraLocations.ProgramFolder("tools"), "mfcapture.log", true));
            trace.AssociateSource(source);

            RootCommand rootCommand = new RootCommand("An application to communciate with Video Capture Devices");

            var jsonOption = new System.CommandLine.Option<bool>("--json", "Structure output data in .json format.")
            {
                Arity = ArgumentArity.Zero,
                IsRequired = false
            };

            var outputFileOption = new System.CommandLine.Option<string>("--outputFile", "Output data to the specified file.")
            {
                IsRequired = false,
                Arity = ArgumentArity.ExactlyOne
            };

            rootCommand.AddGlobalOption(jsonOption);
            rootCommand.AddGlobalOption(outputFileOption);

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

            listDevicesCommand.SetHandler(ListCaptureDevices, jsonOption, outputFileOption);

            rootCommand.AddCommand(listDevicesCommand);

            /*
             * List Video Types for a capture device
             */
            var deviceNameOption = new System.CommandLine.Option<string>("--deviceName", description: "The friendly name given from a capture device. Acquired from the \"devices\" command.")
            {
                IsRequired = true,
                Arity = ArgumentArity.ExactlyOne
            };

            var listMediaTypesCommand = new Command("mediaTypes",
                "List all media types for a given capture device. " +
                "Each media source has an encoding and an image Resolution. " +
                "The encodings are referenced with their Four CC code - details about which can be found online. " +
                "The codec selection is important due to compression amount and type. " +
                Environment.NewLine + Environment.NewLine +
                "After identifying the appropriate media source for some device, capture an image using the \"capture\" command.")
            {
                deviceNameOption,
            };

            listMediaTypesCommand.SetHandler(ListMediaTypes, deviceNameOption, jsonOption, outputFileOption);

            rootCommand.AddCommand(listMediaTypesCommand);

            /*
             * Capture a still image from a capture device
             */
            var mediaTypeOption = new System.CommandLine.Option<string>("--mediaType", description: "The Media Source ID to select for media playback, acquired through the \"mediaType\" command.");
            var filepathOption = new System.CommandLine.Option<string>("--filepath",
                description: "A save location for the captured sample. " +
                "This filepath must point to a file location within an already existing directory."
            )
            {
                Arity = ArgumentArity.ExactlyOne,
                IsRequired = true
            };
            var captureCommand = new Command("capture", "Capture a still image from a given capture device and media type")
            {
                deviceNameOption,
                mediaTypeOption,
                filepathOption
            };
            captureCommand.SetHandler(CaptureImage, deviceNameOption, mediaTypeOption, filepathOption);
            rootCommand.AddCommand(captureCommand);

            var result = rootCommand.Invoke(args);

            using var outputStream = OutputFile switch
            {
                null => Console.Out,
                string value => new StreamWriter(File.OpenWrite(value))
            };

            outputStream.Write(OutputText);
        }

        private static void CaptureImage(string deviceName, string mediaTypeId, string filepath)
        {
            var device = SelectVidcapDevice(deviceName);
            if (device is null)
            {
                SetExitCode(ExitCodes.DeviceNotFound);
                return;
            }

            var maybeReader = GetDefaultSourceReader(device).WithException(ExitCodes.UnexpectedError);
            var maybeSample = maybeReader.FlatMap(sourceReader =>
            {
                var maybeMedia = SelectMediaType(sourceReader, mediaTypeId).WithException(ExitCodes.MediaTypeNotFound);
                return maybeMedia.FlatMap(mediaType =>
                {
                    // Media type was found
                    sourceReader.SetMediaType(mediaType);

                    // The initial frames might be poorly exposed. Skip a few frames to get a good exposure
                    int skipFrameCount = 1;
                    for (int i = 0; i < skipFrameCount; i++)
                    {
                        sourceReader.ReadSample().MatchSome(sample =>
                        {
                            Console.Error.WriteLine($"Sample {i} skipped");
                            sample.Dispose();
                        });
                    }

                    return sourceReader.ReadSample().WithException(ExitCodes.UnexpectedError);
                });
            });

            var exitCode = maybeSample.Match<ExitCodes>(
                some: sample =>
                {
                    var maybeBmp = sample.GetBitmap();
                    sample.Dispose();
                    return maybeBmp.Match(
                        some: bmp =>
                        {
                            try
                            {
                                bmp.Save(filepath);
                                Console.Error.WriteLine($"Bitmap saved to file: {filepath}");
                                return ExitCodes.None;
                            }
                            catch (Exception e)
                            {
                                TracerST.Instance.Error(ExceptionMessage.Handled(e, $"Failed to write bitmap"));
                                Console.Error.WriteLine($"Failed to write bitmap to file.");
                                return ExitCodes.UnexpectedError;
                            }
                        },
                        none: () =>
                        {
                            Console.Error.WriteLine($"Failed to read bitmap from sample.");
                            return ExitCodes.UnexpectedError;
                        }
                    );
                },
                none: error =>
                {
                    Console.Error.WriteLine($"Could not read sample from capture device");
                    return error;
                });

            SetExitCode(exitCode);

            return;
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

        private static void HandleGlobalOptions(bool useJson, string outputFilepath)
        {
            UseJson = useJson;
            OutputFile = outputFilepath;
        }

        private static void ListCaptureDevices(bool useJson, string outputFilePath)
        {
            HandleGlobalOptions(useJson, outputFilePath);

            var devices = GetCaptureDeviceDTOs();
            if (!UseJson)
            {
                foreach (var d in devices)
                {
                    OutputText += $"{d.FriendlyName} : {d.SymbolicName}{Environment.NewLine}";
                }
            }
            else
            {
                OutputText = JsonConvert.SerializeObject(devices, Formatting.Indented);
            }

            SetExitCode(ExitCodes.None);
        }

        /// <summary>
        /// Main-line command to list all of the media types for a given device / media source
        /// </summary>
        /// <param name="deviceName">The devicename to select</param>
        /// <param name="useJson">Output media type information as structured json. If false, return elements separated by newlines</param>
        /// <param name="outputFilePath">Output media type information to the file selected</param>
        private static void ListMediaTypes(string deviceName, bool useJson, string outputFilePath)
        {
            HandleGlobalOptions(useJson, outputFilePath);

            var device = SelectVidcapDevice(deviceName);
            if (device is null)
            {
                SetExitCode(ExitCodes.DeviceNotFound);
                return;
            }

            var maybeMediaTypes = GetMediaTypeDTOs(device);

            maybeMediaTypes.MatchSome(mediaTypes =>
            {
                if (!UseJson)
                {
                    foreach (var m in mediaTypes)
                    {
                        OutputText += m.ID + Environment.NewLine;
                    }
                }
                else
                {
                    OutputText = JsonConvert.SerializeObject(mediaTypes, Formatting.Indented);
                }
            });
            SetExitCode(ExitCodes.None);
        }

        private static Optional.Option<MediaTypeWrapper> SelectMediaType(SourceReaderWrapper device, string mediaTypeId)
        {
            foreach (var m in device.MediaTypes)
            {
                // use the dto to generate the ID for now
                var dto = MediaTypeDTO.Create(m);
                if (Equals(dto.ID, mediaTypeId))
                {
                    Console.Error.WriteLine($"Found media type for ID: {mediaTypeId}");
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

            if (device is null)
            {
                Console.Error.WriteLine($"Device was not found for friendly name: {deviceFriendlyName}. Get a list of all devices using the \"devices\" command.");
                return null;
            }
            else
            {
                Console.Error.WriteLine($"Device found for name: {deviceFriendlyName}");
                return device;
            }
        }

        private static void SetExitCode(ExitCodes code)
        {
            Environment.ExitCode = (int)code;
        }
    }
}