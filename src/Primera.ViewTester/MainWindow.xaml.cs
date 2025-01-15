using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;

using Primera.Common.Logging;
using Primera.Webcam.Device;

namespace Primera.ViewTester
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public MainWindow()
        {
            InitializeComponent();

            var traceSource = new TraceSource("ViewTester")
            {
                Switch = new SourceSwitch("ViewTesterSwitch")
                {
                    Level = SourceLevels.Verbose
                }
            };

            var trace = TracerST.Instance;
            trace.AssociateSource(traceSource);
            CameraCaptureTracing.RegisterTrace(trace);
        }

        public static ITrace Trace => TracerST.Instance;
    }
}