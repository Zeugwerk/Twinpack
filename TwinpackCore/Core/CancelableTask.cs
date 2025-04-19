using System.Threading;
using System;
using System.Threading.Tasks;

public class CancelableTask
{
    private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

    private CancellationTokenSource _cts;
    private Task _task;

    public void Cancel()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    public async Task RunAsync(Func<CancellationToken, Task> action, Action onFinally = null)
    {
        _cts?.Cancel();

        if (_task != null)
        {
            try
            {
                await _task;
            }
            catch { }
        }

        var wasCanceled = false;
        try
        {
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            _task = action(_cts.Token);

            _cts.Token.ThrowIfCancellationRequested();
            await _task;
            _cts.Token.ThrowIfCancellationRequested();

            _task = null;
        }
        catch (OperationCanceledException ex)
        {
            wasCanceled = true;
            _logger.Trace(ex);
        }
        catch (Exception ex)
        {
            _logger.Trace(ex);
            _logger.Error(ex.Message);
        }
        finally
        {
            onFinally?.Invoke();
        }
    }
}