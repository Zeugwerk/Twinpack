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
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Threading;
using NLog;
using NLog.Config;
using NLog.Targets;
using Task = System.Threading.Tasks.Task;

namespace Twinpack
{
    [Target("VsOutputWindowTarget")]
    public class VsOutputWindowTarget : AsyncTaskTarget
    {
        private static Guid OutputPaneGuid = new Guid("E12CEAA1-6466-4841-8A69-9D4E96638CD8");
        private IVsOutputWindowPane _outputPane;

        public async Task ActivateAsync(bool clear)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (clear)
                _outputPane?.Clear();

            _outputPane?.Activate();

            Guid outputWindowGuid = new Guid(ToolWindowGuids80.Outputwindow);
            IVsWindowFrame frame = null;
            IVsUIShell uiShell = Package.GetGlobalService(typeof(SVsUIShell)) as IVsUIShell;
            uiShell?.FindToolWindow((uint)__VSFINDTOOLWIN.FTW_fForceCreate, outputWindowGuid, out frame);
            frame?.Show();
        }

        public async Task LogToOutputWindowAsync(string message)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (_outputPane == null)
            {
                IVsOutputWindow outputWindow = Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;
                if (outputWindow != null)
                {
                    outputWindow.GetPane(ref OutputPaneGuid, out _outputPane);

                    if(_outputPane == null)
                    {
                        // Create the custom output pane (if it doesn't exist) and get the pane object.
                        int result = outputWindow.CreatePane(OutputPaneGuid, "Twinpack Package Manager", 1, 1);
                        if (result == VSConstants.S_OK)
                        {
                            outputWindow.GetPane(ref OutputPaneGuid, out _outputPane);
                        }
                    }
                }
            }

            if (_outputPane != null)
                _outputPane.OutputStringThreadSafe(message + Environment.NewLine);
        }

        protected override async Task WriteAsyncTask(LogEventInfo logEvent, CancellationToken cancellationToken)
        {
            string logMessage = Layout.Render(logEvent);

            if (logEvent.Level == LogLevel.Error)
                await ActivateAsync(clear: false);

            await LogToOutputWindowAsync(logMessage);
        }
    }
}
