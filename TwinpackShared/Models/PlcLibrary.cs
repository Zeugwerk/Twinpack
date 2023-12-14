using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Twinpack.Models
{
    public class PlcLibrary
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string DistributorName { get; set; }

        public static bool operator ==(PlcLibrary lhs, PlcLibrary rhs)
        {
            return lhs?.Name == rhs?.Name && lhs?.Name == rhs?.Name && lhs?.Version == rhs?.Version;
        }

        public static bool operator !=(PlcLibrary lhs, PlcLibrary rhs)
        {
            return !(lhs == rhs);
        }

        public override bool Equals(object o)
        {
            return this == o as PlcLibrary;
        }

        public override int GetHashCode()
        {
            return 17 + Name?.GetHashCode() ?? 0 + 23 * Version?.GetHashCode() ?? 0 + 23 * DistributorName?.GetHashCode() ?? 0;
        }
    }
}
