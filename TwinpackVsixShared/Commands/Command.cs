using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;

namespace Twinpack.Commands
{
    /// <summary>
    /// Base Class for all menus and commands of the Zeugwerk Creator Extension
    /// </summary>
    public abstract class Command : ICommand
    {
        public static readonly Guid CommandSet = new Guid("0ee4f42e-82af-4eba-92b9-bc7691b0de49");
        protected static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        protected readonly TwinpackPackage _package;
        protected MenuCommand _menuCommand;

        public Command(TwinpackPackage package)
        {
            _package = package;
        }

        public MenuCommand MenuItem
        {
            get { return _menuCommand; }
        }
        public void PackageReady()
        {
            // package is initialized, enable functions of the command
            if (_package.IsInitialized)
            {
                _menuCommand.Visible = true;
                _menuCommand.Enabled = true;
            }
        }
        public virtual void PackageReset()
        {
            _menuCommand.Visible = true;
            _menuCommand.Enabled = true;
        }
    }
}
