using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using NLog;
using System;
using System.ComponentModel.Design;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Task = System.Threading.Tasks.Task;

namespace Twinpack.Commands
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class CatalogCommand : Command, ICommand
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 260;

        /// <summary>
        /// Initializes a new instance of the <see cref="BackuppanelCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private CatalogCommand(TwinpackPackage package, OleMenuCommandService commandService)
          : base(package)
        {
            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID)
            {
                Visible = true,
                Enabled = true
            };
            commandService.AddCommand(menuItem);

            _menuCommand = menuItem;
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static CatalogCommand Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(TwinpackPackage package)
        {
            // Switch to the main thread - the call to AddCommand in BackuppanelCommand's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new CatalogCommand(package, commandService);
        }

        /// <summary>
        /// Shows the tool window when the menu item is clicked.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event args.</param>
#pragma warning disable VSTHRD100 // "async void"-Methoden vermeiden
        private async void Execute(object sender, EventArgs e)
#pragma warning restore VSTHRD100 // "async void"-Methoden vermeiden
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);

                ToolWindowPane window = _package.FindToolWindow(typeof(Dialogs.CatalogPane), 0, true);
                if ((null == window) || (null == window.Frame))
                {
                    throw new NotSupportedException("Cannot create tool window");
                }

                (window as Dialogs.CatalogPane).Update();
                IVsWindowFrame windowFrame = (IVsWindowFrame)window.Frame;
                Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(windowFrame.Show());
            }
            catch(Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
        }

        public override void PackageReset()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            ToolWindowPane window = _package.FindToolWindow(typeof(Dialogs.CatalogPane), 0, false);

            if(window != null)
            {
                ((window as Dialogs.CatalogPane)?.Frame as IVsWindowFrame).Hide();
            }
            base.PackageReset();
        }
    }
}
