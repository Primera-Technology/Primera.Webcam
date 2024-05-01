using System;
using System.Collections.Generic;
using System.Linq;

using MediaFoundation;
using MediaFoundation.Misc;

using Primera.Common.Logging;
using Primera.Webcam.Device;

namespace CameraCapture.WPF.VideoCapture
{
    public class CaptureHelper
    {
        public static object SampleLock = new();

        public static ITrace Trace => CameraCaptureTracing.Trace;

        public static IList<IMFActivate> EnumerateMfDevices(Guid filterCategory)
        {
            string logDetail = $" [Category: {filterCategory}]";
            IMFAttributes attributes = null;
            try
            {
                Trace.Info($"Enumerating devices." + logDetail);
                Trace.Verbose("Creating attributes object.");
                MFExtern.MFCreateAttributes(out attributes, 1).CheckResult();

                Trace.Verbose("Setting filter category.");
                attributes.SetGUID(MFAttributesClsid.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE, filterCategory).CheckResult();

                Trace.Verbose("Enumerating device sources.");
                MFExtern.MFEnumDeviceSources(attributes, out IMFActivate[] devices, out int numberDevices).CheckResult();

                if (devices is null)
                {
                    Trace.Warning($"No devices returned from enumeration");
                    return new List<IMFActivate>();
                }

                logDetail += $" [Count: {devices.Length}]";
                Trace.Info("Devices enumerated." + logDetail);
                return devices.ToList();
            }
            catch (HResultException e)
            {
                Trace.Error(ExceptionMessage.Handled(e, "Failed to enumerate Media Foundation Devices." + logDetail));
                return new List<IMFActivate>();
            }
            finally
            {
                COMBase.SafeRelease(attributes);
            }
        }
    }
}