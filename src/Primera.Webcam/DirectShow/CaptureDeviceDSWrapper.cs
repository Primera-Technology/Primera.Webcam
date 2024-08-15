using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.ComTypes;

using DirectShowLib;

namespace Primera.Webcam.DirectShow
{
    public class CaptureDeviceDSWrapper
    {
        private int _exposureCurrent;
        private CameraControlFlags _exposureFlagCurrent;

        public CaptureDeviceDSWrapper(IBaseFilter filter, string friendlyName, string devicePath)
        {
            Filter = filter;
            FriendlyName = friendlyName;
            DevicePath = devicePath;

            CameraControl = filter as IAMCameraControl;

            CameraControl.GetRange(CameraControlProperty.Exposure, out int min, out int max, out int step, out int def, out CameraControlFlags flags);
            ExposureMin = min;
            ExposureMax = max;
            ExposureStep = step;
            ExposureDefault = def;
            ExposureFlagSelection = flags;

            CameraControl.Get(CameraControlProperty.Exposure, out int exposure, out CameraControlFlags exposureFlags);
            _exposureCurrent = exposure;
            _exposureFlagCurrent = exposureFlags;
        }

        public IAMCameraControl CameraControl { get; }

        public string DevicePath { get; }

        public int ExposureCurrent
        {
            get => _exposureCurrent;
            set
            {
                CameraControl.Set(CameraControlProperty.Exposure, value, CameraControlFlags.Manual);
                _exposureCurrent = value;
            }
        }

        public int ExposureDefault { get; }

        public CameraControlFlags ExposureFlagCurrent
        {
            get => _exposureFlagCurrent;
            set
            {
                CameraControl.Set(CameraControlProperty.Exposure, _exposureCurrent, value);
                _exposureFlagCurrent = value;
            }
        }

        public CameraControlFlags ExposureFlagSelection { get; }
        public int ExposureMax { get; }
        public int ExposureMin { get; }
        public int ExposureStep { get; }
        public IBaseFilter Filter { get; }
        public string FriendlyName { get; }

        public static IEnumerable<CaptureDeviceDSWrapper> EnumerateVideoDevices()
        {
            Guid filterCategory = FilterCategory.VideoInputDevice;
            ICreateDevEnum devEnum = (ICreateDevEnum)new CreateDevEnum();
            devEnum.CreateClassEnumerator(filterCategory, out IEnumMoniker enumMoniker, 0);
            IntPtr fetched = IntPtr.Zero;
            IMoniker[] monikers = new IMoniker[1];

            Guid filterInterface = typeof(IBaseFilter).GUID;
            Guid propertyBagInterface = typeof(IPropertyBag).GUID;

            while (enumMoniker.Next(1, monikers, fetched) == 0)
            {
                var moniker = monikers[0];
                moniker.BindToStorage(null, null, ref propertyBagInterface, out object pBag);
                IPropertyBag propertyBag = (IPropertyBag)pBag;
                propertyBag.Read("FriendlyName", out object friendlyName, null);
                propertyBag.Read("DevicePath", out object devicePath, null);
                propertyBag.Read("Description", out object description, null);

                moniker.BindToObject(null, null, ref filterInterface, out object filter);

                var wrapper = new CaptureDeviceDSWrapper((IBaseFilter)filter, (string)friendlyName, (string)devicePath);
                yield return wrapper;
            }
        }
    }
}