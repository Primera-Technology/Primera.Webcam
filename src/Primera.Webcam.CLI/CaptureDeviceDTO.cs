namespace Primera.Webcam.CLI
{
    public record CaptureDeviceDTO
    {
        public CaptureDeviceDTO(string friendlyName, string symbolicName)
        {
            FriendlyName = friendlyName;
            SymbolicName = symbolicName;
        }

        public string FriendlyName { get; }

        public string SymbolicName { get; }
    }
}