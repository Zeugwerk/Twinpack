using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Twinpack.Commands
{
    internal interface ICommand
    {
        void PackageReady();
        void PackageReset();
    }
}
