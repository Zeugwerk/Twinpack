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
    internal sealed class TwinpackMenuCommand : Command, ICommand
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 263;

        /// <summary>
        /// Initializes a new instance of the <see cref="BackuppanelCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private TwinpackMenuCommand(TwinpackPackage package, OleMenuCommandService commandService)
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
        public static TwinpackMenuCommand Instance
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
            Instance = new TwinpackMenuCommand(package, commandService);
        }

        /// <summary>
        /// Shows the tool window when the menu item is clicked.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event args.</param>
#pragma warning disable CS1998 // Bei der asynchronen Methode fehlen "await"-Operatoren. Die Methode wird synchron ausgeführt.
#pragma warning disable VSTHRD100 // "async void"-Methoden vermeiden
        private async void Execute(object sender, EventArgs e)
#pragma warning restore VSTHRD100 // "async void"-Methoden vermeiden
#pragma warning restore CS1998 // Bei der asynchronen Methode fehlen "await"-Operatoren. Die Methode wird synchron ausgeführt.
        {

        }
    }
}
