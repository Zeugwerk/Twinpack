using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Twinpack.Models
{
    public class PlcVersion
    {
        public string Version { get; set; }
        public string VersionDisplayText { get; set; }
        public bool IsWildcard { get; set; }

        public static bool operator ==(PlcVersion lhs, PlcVersion rhs)
        {
            return lhs?.Version == rhs?.Version &&
                lhs?.VersionDisplayText == rhs?.VersionDisplayText;
        }

        public static bool operator !=(PlcVersion lhs, PlcVersion rhs)
        {
            return !(lhs == rhs);
        }

        public override bool Equals(object o)
        {
            return this == o as PlcVersion;
        }

        public override int GetHashCode()
        {
            return 23 * Version?.GetHashCode() + 48 * VersionDisplayText?.GetHashCode() ?? 0;
        }
    }
}
