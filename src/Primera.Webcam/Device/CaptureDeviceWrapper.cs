using System;
using System.Collections.Generic;
using System.Linq;

using CameraCapture.WPF.VideoCapture;

using MediaFoundation;
using MediaFoundation.Misc;

using Optional;

using Primera.Common.Logging;

namespace Primera.Webcam.Device
{

    /// <summary>
    /// A Capture Device is a Media Foundation device that enumerates itself using a special GUID.
    /// The source object, accessible through <see cref="Instance"/>, is considered "unactivated" at first.
    /// It contains information about the device and how it enumerates in Windows, but to capture video must be activated using <see cref="Activate"/>.
    /// <para/>
    /// To begin working with Media Foundation in this library, get the list of all available video capture devices with <see cref="GetAllDevices"/>.
    /// </summary>
    public class CaptureDeviceWrapper : IDisposable
    {
        private CaptureDeviceWrapper(IMFActivate instance)
        {
            Instance = instance;
        }

        public static ITrace Trace => CameraCaptureTracing.Trace;

        /// <summary>
        /// The Media Foundation COM object to be manipulated
        /// </summary>
        public IMFActivate Instance { get; }

        /// <summary>
        /// A factory method to enumerate all of the Video Capture devices available.
        /// </summary>
        public static IReadOnlyList<CaptureDeviceWrapper> GetAllDevices()
        {
            var devices = CaptureHelper.EnumerateMfDevices(CLSID.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_GUID);

            return devices.Select(Create).ToList();
        }

        /// <summary>
        /// Activate this instance. See <see cref="MediaSourceWrapper"/> for further processing steps.
        /// </summary>
        /// <returns>This instance may not activate properly, if it fails, returns nothing.</returns>
        public Option<MediaSourceWrapper> Activate()
        {
            Trace.Verbose("Activating new source capture device.");
            HResult result = Instance.ActivateObject(typeof(IMFMediaSource).GUID, out object o);

            if (COMBase.Failed(result))
            {
                return Option.None<MediaSourceWrapper>();
            }
            else if (o is not IMFMediaSource mediaSource)
            {
                return Option.None<MediaSourceWrapper>();
            }
            else
            {
                return new MediaSourceWrapper(mediaSource).Some();
            }
        }

        public void Dispose()
        {
            COMBase.SafeRelease(Instance);
        }

        /// <summary>
        /// Attempts to get the name most frequently displayed to users that was assigned to this device.
        /// <para/>
        /// e.g. "USB Camera"
        /// </summary>
        public Option<string> GetFriendlyName()
        {
            try
            {
                Instance.GetAllocatedString(MFAttributesClsid.MF_DEVSOURCE_ATTRIBUTE_FRIENDLY_NAME, out string friendlyName, out int friendlyLength).CheckResult();
                return friendlyName.Some();
            }
            catch
            {
                return Option.None<string>();
            }
        }

        /// <summary>
        /// Attempts to get a unique symbolic name assigned to the device.
        /// This includes device information like VIDPID, Usb Serial Name, Device ID, etc.
        /// </summary>
        public Option<string> GetSymbolicName()
        {
            try
            {
                Instance.GetAllocatedString(MFAttributesClsid.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_SYMBOLIC_LINK, out string symbolicName, out int symbolicLength).CheckResult();
                return symbolicName.Some();
            }
            catch
            {
                return Option.None<string>();
            }
        }

        private static CaptureDeviceWrapper Create(IMFActivate deviceObject)
        {
            return new CaptureDeviceWrapper(deviceObject);
        }
    }
}