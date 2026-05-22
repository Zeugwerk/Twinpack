using NLog;

namespace Twinpack
{
    /// <summary>Phase banners and summaries for Twinpack CLI runs (grep-friendly).</summary>
    public static class TwinpackRunLog
    {
        public const int SeparatorWidth = 73;

        public static string Separator => new string('-', SeparatorWidth);

        public static void LogBanner(Logger logger, string label, string subtitle)
            => logger.Info("=== [{0}] phase: {1}", label, subtitle);

        public static void LogPhaseDone(Logger logger, string label, double elapsedSeconds, bool failed = false)
            => logger.Info("[{0}] done in {1:F2}s ({2})", label, elapsedSeconds, failed ? "failed" : "ok");

        public static void LogSummary(Logger logger, params (string name, double seconds)[] phases)
        {
            var parts = new System.Collections.Generic.List<string>();
            double sum = 0;
            foreach (var (name, seconds) in phases)
            {
                if (seconds < 0) continue;
                parts.Add($"{name} {seconds:F2}s");
                sum += seconds;
            }

            logger.Info("=== [summary] {0} | sum {1:F2}s", string.Join(" | ", parts), sum);
        }

        /// <summary>Single-line run outcome (grep <c>=== [result]</c>; SUCCESS is highlighted green in NLog).</summary>
        public static void LogResult(Logger logger, bool success)
            => logger.Info(success ? "=== [result] SUCCESS" : "=== [result] FAILED");
    }
}
