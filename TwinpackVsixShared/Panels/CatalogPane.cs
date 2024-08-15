using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Data;

namespace Twinpack.Dialogs
{
    /// <summary>
    /// This class implements the tool window exposed by this package and hosts a user control.
    /// </summary>
    /// <remarks>
    /// In Visual Studio tool windows are composed of a frame (implemented by the shell) and a pane,
    /// usually implemented by the package implementer.
    /// <para>
    /// This class derives from the ToolWindowPane class provided from the MPF in order to use its
    /// implementation of the IVsUIElementPane interface.
    /// </para>
    /// </remarks>
    [Guid("41e9fc85-6fd5-4cfb-86cc-808fb1ebdbf9")]
    public class CatalogPane : ToolWindowPane
    {
        private CatalogWindow _catalogWindow;
        private PackageContext _context;
        public CatalogPane(PackageContext context) : base(null)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _context = context;
            _catalogWindow = new CatalogWindow(_context);

            Content = _catalogWindow;
        }

        public async System.Threading.Tasks.Task InitializeAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var plc = _context.VisualStudio.ActivePlc();
            await _catalogWindow.InitializeAsync();

            Caption = $"Twinpack: {plc?.Name ?? "No Context"}";
        }
    }
}