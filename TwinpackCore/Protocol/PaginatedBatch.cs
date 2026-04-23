using System.Collections.Generic;
using System.Linq;

namespace Twinpack.Protocol
{
    /// <summary>
    /// One page of results from a paged catalog or package-version listing (<c>HasMorePages</c> indicates another request may return more rows).
    /// </summary>
    public sealed class PaginatedBatch<T>
    {
        public PaginatedBatch(IEnumerable<T> items, bool hasMorePages)
        {
            Items = (items ?? Enumerable.Empty<T>()).ToList();
            HasMorePages = hasMorePages;
        }

        public IReadOnlyList<T> Items { get; }

        public bool HasMorePages { get; }
    }
}
