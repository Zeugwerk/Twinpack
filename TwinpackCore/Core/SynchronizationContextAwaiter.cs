using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Twinpack.Core
{
    public struct SynchronizationContextAwaiter : INotifyCompletion, IEquatable<SynchronizationContextAwaiter>
    {
        private static readonly SendOrPostCallback postCallback = state => (state as Action)?.Invoke();

        private readonly SynchronizationContext context;

        public SynchronizationContextAwaiter(SynchronizationContext context) => this.context = context;

        public bool IsCompleted => context == SynchronizationContext.Current;

        public static bool operator !=(SynchronizationContextAwaiter left, SynchronizationContextAwaiter right) =>
            !(left == right);

        public static bool operator ==(SynchronizationContextAwaiter left, SynchronizationContextAwaiter right) =>
            left.Equals(right);

        public override bool Equals(object obj) =>
            obj is SynchronizationContextAwaiter awaiter && this.Equals(awaiter);

        public bool Equals(SynchronizationContextAwaiter other) =>
            EqualityComparer<SynchronizationContext>.Default.Equals(this.context, other.context);

        public override int GetHashCode() => HashCode.Combine(this.context);

        [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Reviewed.")]
        public void GetResult() {}

        public void OnCompleted(Action continuation)
        {
            context.Post(postCallback, continuation);
        }
    }

    public static class SynchronizationContextAwaiterExtensions
    {
        public static SynchronizationContextAwaiter GetAwaiter(this SynchronizationContext context) =>
                    new SynchronizationContextAwaiter(context);
    }
    
}
