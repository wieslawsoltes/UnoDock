namespace UnoDock.Controls;

/// <summary>
/// Lifecycle for a composed docking window. Initialization is delayed until the
/// derived model has been assigned; unloading/rehosting is not permanent closure.
/// </summary>
public abstract class DockWindowControl : ContentControl
{
    private bool _initialized, _closed;
    protected DockWindowControl() => Loaded += (_, _) => EnsureInitialized();
    public event EventHandler? Initialized;
    public event EventHandler<CancelEventArgs>? Closing;
    public event EventHandler? Closed;
    public event EventHandler? StateChanged;
    protected bool IsWindowClosed => _closed;
    protected void EnsureInitialized()
    {
        if (_initialized || _closed) return;
        // Commit before user callbacks so reentrant refresh cannot initialize twice.
        _initialized = true;
        OnInitialized(EventArgs.Empty);
    }
    protected virtual void OnInitialized(EventArgs e) => Initialized?.Invoke(this, e);
    protected virtual void OnClosing(CancelEventArgs e) => Closing?.Invoke(this, e);
    protected virtual void OnClosed(EventArgs e) => Closed?.Invoke(this, e);
    protected virtual void OnStateChanged(EventArgs e) => StateChanged?.Invoke(this, e);
    protected void CompleteWindowClose()
    {
        if (_closed) return;
        _closed = true;
        OnClosed(EventArgs.Empty);
    }
}
