using System;

public static class HeartbeatService
{
    private static readonly object _lock = new object();

    private static System.Threading.Timer? _timer;
    private static DateTime _lastHeartbeat;
    private static bool _started;
    private static bool _initialized;    

    private static TimeSpan _timeout;
    private static Action? _onTimeout;
    private static NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

    public static void Initialize(TimeSpan timeout, Action onTimeout)
    {
        lock (_lock)
        {
            _timeout = timeout;
            _onTimeout = onTimeout;
            _started = false;
            _initialized = true;
        }
    }

    /// <summary>
    /// Call this every time a heartbeat (ProgressedEvent) is received.
    /// Starts the timer on the first call.
    /// </summary>
    public static void Beat()
    {
        if (!_initialized)
            return;
        
        lock (_lock)
        {
            _lastHeartbeat = DateTime.Now;

            if (!_started)
            {
                _timer = new System.Threading.Timer(OnTimeout, null, _timeout, System.Threading.Timeout.InfiniteTimeSpan);
                _started = true;
                
                _logger?.Info(
                    "HeartbeatService: First heartbeat received — starting timeout watchdog ({Timeout} min).",
                    _timeout.TotalMinutes);
            }
            else
            {
                _timer!.Change(_timeout, System.Threading.Timeout.InfiniteTimeSpan);
            }
        }
    }

    private static void OnTimeout(object? state)
    {        
        _logger?.Warn(
            "HeartbeatService: No heartbeat received for {Timeout} minutes. " +
            "Last heartbeat was at {LastHeartbeat} ({Elapsed:mm\\:ss} ago). " +
            "TwinCAT XAEShell / Visual Studio appears unresponsive — triggering kill.",
            _timeout.TotalMinutes,
            _lastHeartbeat,
            DateTime.Now - _lastHeartbeat);

        _onTimeout?.Invoke();
        KillProcess();
    }

    private static void KillProcess()
    {
        Environment.Exit(-1);

        string processName = AppDomain.CurrentDomain.FriendlyName;
        System.Diagnostics.Process[] processes = System.Diagnostics.Process.GetProcessesByName(processName);
        foreach (System.Diagnostics.Process p in processes)
        {
            p.Kill();
        }
    }

    public static void Stop()
    {
        if (!_initialized)
            return;

        lock (_lock)
        {
            _timer?.Dispose();
            _timer = null;
            _started = false;
        }
    }
}
