using MediaFoundation;

using CameraCapture.WPF.VideoCapture;

namespace CameraCapture.WPF.VideoCapture
{
    public class ActivationObject
    {
        public ActivationObject(IMFActivate instance)
        {
            Instance = instance;
        }

        public T Activate<T>()
        {
            Instance.ActivateObject(typeof(T).GUID, out object value).CheckResult();
            return (T)value;
        }

        public IMFActivate Instance { get; }
    }
}