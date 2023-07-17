using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;
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
        public CatalogPane(PackageContext context) : base(null)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var catalogWindow = new CatalogWindow(context);
            Content = catalogWindow;

            EnvDTE.Project plc = null;
            if (context.Dte.ActiveSolutionProjects is Array activeSolutionProjects && activeSolutionProjects.Length > 0)
                plc = activeSolutionProjects.GetValue(0) as EnvDTE.Project;

            Caption = $"Twinpack Catalog {(plc != null ? " - " + plc.Name : "")}";
        }
    }
}