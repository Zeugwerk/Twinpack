using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using NLog;
using NLog.Config;
using NLog.Targets;
using Task = System.Threading.Tasks.Task;

namespace Twinpack
{
    [Target("VisualStudioOutput")]
    public class VisualStudioOutput : AsyncTaskTarget
    {
        private static Guid OutputPaneGuid = new Guid("E12CEAA1-6466-4841-8A69-9D4E96638CD8");
        private static IVsOutputWindowPane _outputPane;

        public static void Activate()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _outputPane?.Activate();
        }

        public async Task LogToOutputWindowAsync(string message)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

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

        protected override Task WriteAsyncTask(LogEventInfo logEvent, CancellationToken cancellationToken)
        {
            string logMessage = this.Layout.Render(logEvent);
            return LogToOutputWindowAsync(logMessage);
        }
    }
}
