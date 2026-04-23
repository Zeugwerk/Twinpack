using System;

namespace Twinpack.Models
{
    /// <summary>
    /// Helpers for comparing <see cref="PackageItem"/> instances that refer to the same persisted <see cref="Configuration.PlcPackageReference"/> (same PLC + package name).
    /// </summary>
    public static class PackageItemReference
    {
        public static bool SameConfiguredReference(PackageItem candidate, PackageItem used)
        {
            if (candidate == null || used == null)
                return false;

            return string.Equals(candidate.ProjectName, used.ProjectName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(candidate.PlcName, used.PlcName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(candidate.PlcPackageReference?.Name, used.PlcPackageReference?.Name, StringComparison.OrdinalIgnoreCase);
        }
    }
}
