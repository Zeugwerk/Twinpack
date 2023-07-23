using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NLog;
using NLog.Targets;

namespace Twinpack
{
    [Target("VisualStudioOutput")]
    public class VisualStudioOutput : TargetWithLayout
    {
        private static Guid OutputPaneGuid = new Guid("E12CEAA1-6466-4841-8A69-9D4E96638CD8");
        private static IVsOutputWindowPane _outputPane;

        public static void Activate()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _outputPane?.Activate();
        }

        public void LogToOutputWindow(string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_outputPane == null)
            {
                IVsOutputWindow outputWindow = Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;
                if (outputWindow != null)
                {
                    // Create the custom output pane (if it doesn't exist) and get the pane object.
                    int result = outputWindow.CreatePane(OutputPaneGuid, "Twinpack", 1, 1);
                    if (result == VSConstants.S_OK)
                    {
                        outputWindow.GetPane(ref OutputPaneGuid, out _outputPane);
                    }
                }
            }

            if (_outputPane != null)
            {
                _outputPane.OutputString(message + Environment.NewLine);
            }
        }
        protected override void Write(LogEventInfo logEvent)
        {
            string logMessage = Layout.Render(logEvent);
            LogToOutputWindow(logMessage);
        }
    }
}
