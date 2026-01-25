using System.Threading;
using System;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;

public class CancelableTask : INotifyPropertyChanged
{
    private NLog.Logger _logger;

    private CancellationTokenSource _cts;
    private Task _task;
    public event PropertyChangedEventHandler PropertyChanged;

    public CancelableTask(NLog.Logger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool _isBusy;
    public bool Busy
    {
        get => _isBusy;
        set
        {
            if (_isBusy != value)
            {
                _isBusy = value;
                OnPropertyChanged();
            }
        }
    }

    public void Cancel()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    public async Task RunAsync(Func<CancellationToken, Task> action, Func<Task> onFinally = null)
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
            Busy = true;

            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            _task = action(_cts.Token);

            _cts.Token.ThrowIfCancellationRequested();

            if (_task != null)
            {
                await _task;
            }
            
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
            if (onFinally != null)
                await onFinally();

            Busy = false;
        }
    }
}
