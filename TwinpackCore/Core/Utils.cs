using NLog;
using System;
using System.Threading;

namespace Twinpack.Core
{
    class Utils
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        public static void Retry(
            Action action,
            int maxRetries,
            int baseDelayMs = 1000,
            Type? retryOnExceptionType = null,
            string? retryOnMessageContains = null,
            int? maxDelayMs = null)
        {
            int retry = 0;

            while (true)
            {
                try
                {
                    action();
                    return;
                }
                catch (Exception ex)
                {
                    retry++;

                    if (retryOnExceptionType != null &&
                        !retryOnExceptionType.IsInstanceOfType(ex))
                    {
                        throw;
                    }

                    if (!string.IsNullOrEmpty(retryOnMessageContains) &&
                        (ex.Message == null || !ex.Message.Contains(retryOnMessageContains)))
                    {
                        throw;
                    }

                    if (retry >= maxRetries)
                    {
                        throw;
                    }

                    // Exponential backoff
                    int delay = baseDelayMs * (int)Math.Pow(2, retry - 1);

                    if (maxDelayMs.HasValue)
                    {
                        delay = Math.Min(delay, maxDelayMs.Value);
                    }

                    _logger.Info("[retry] {0}/{1} in {2}ms: {3}", retry, maxRetries, delay, ex.Message);

                    Thread.Sleep(delay);
                }
            }
        }

    }
}
