using System.Threading.Tasks;
using Twinpack.Models;

namespace Twinpack.Core
{
    /// <summary>
    /// Installs libraries on this machine for an automation interface that cannot do it itself (headless).
    /// Installing stays opt-in: only a caller that passes an installer gets an interface that installs.
    /// Internal for now; it becomes public together with the shared engineering layer.
    /// </summary>
    internal interface IPackageInstaller
    {
        Task InstallPackageAsync(PackageItem package, string cachePath);
    }
}