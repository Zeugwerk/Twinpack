using System;
using System.Collections.Generic;
using System.Text;

namespace Twinpack.Protocol
{
    public class PackageServerCollection : List<IPackageServer>
    {
        public void InvalidateCache()
        {
            ForEach(x => x.InvalidateCache());
        }
    }
}
