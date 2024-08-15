using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;

using DirectShowLib;

namespace Primera.Webcam.DirectShow
{
    public class CaptureDeviceDSWrapper
    {
        private int _exposureCurrent;
        private CameraControlFlags _exposureFlagCurrent;

        public CaptureDeviceDSWrapper(IBaseFilter filter)
        {
            Filter = filter;
            CameraControl = filter as IAMCameraControl;

            Filter.QueryVendorInfo(out string name);
            FriendlyName = name;

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

        public static IEnumerable<IBaseFilter> CreateFilter(Guid filterCategory)
        {
            ICreateDevEnum devEnum = (ICreateDevEnum)new CreateDevEnum();
            devEnum.CreateClassEnumerator(filterCategory, out IEnumMoniker enumMoniker, 0);
            IntPtr fetched = IntPtr.Zero;
            IMoniker[] moniker = new IMoniker[1];

            Guid filterInterface = typeof(IBaseFilter).GUID;

            while (enumMoniker.Next(1, moniker, fetched) == 0)
            {
                moniker[0].BindToObject(null, null, ref filterInterface, out object filter);
                yield return (IBaseFilter)filter;
            }
        }

        public static IEnumerable<CaptureDeviceDSWrapper> GetAllDevices()
        {
            return CreateFilter(FilterCategory.VideoInputDevice).Select(f => new CaptureDeviceDSWrapper(f));
        }
    }
}