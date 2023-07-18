using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NLog;
using System;
using System.ComponentModel.Design;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Twinpack;
using TCatSysManagerLib;
using Task = System.Threading.Tasks.Task;

namespace Twinpack.Commands
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class PublishCommand : Command, ICommand
    {
        //    private static readonly NLog.Logger _logger = LogManager.GetLogger("Commands.Library");
        private static readonly NLog.Logger _logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 261;

        /// <summary>
        /// Initializes a new instance of the <see cref="BackuppanelCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private PublishCommand(TwinpackPackage package, OleMenuCommandService commandService)
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
        public static PublishCommand Instance
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
            Instance = new PublishCommand(package, commandService);
        }

        /// <summary>
        /// Shows the tool window when the menu item is clicked.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event args.</param>
        private async void Execute(object sender, EventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);

            _logger.Debug("Execute Command");
            var publishWindow = new Dialogs.PublishWindow(_package.Context);
            publishWindow.ShowDialog();
        }
    }
}
