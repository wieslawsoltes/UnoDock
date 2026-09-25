using System.Runtime.InteropServices;

namespace UnoDock.Internal;
/// <summary>Scoped only to an active native drag. AppKit's window-tracking loop
/// requires a common-mode timer; an ordinary dispatcher timer can pause until the
/// mouse is released, which would make docking guides appear only after dragging.</summary>
internal sealed class NativeDragClock : IDisposable
{
    private readonly Action _tick;
    private readonly Action<Exception> _failed;
    private DispatcherTimer? _dispatcherTimer;
    private nint _timer;
    private GCHandle _self;
    private readonly int _thread = Environment.CurrentManagedThreadId;
    private bool _disposed, _invoking;
    private const string AppKit = "/System/Library/Frameworks/AppKit.framework/AppKit";
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private static readonly TimerCallback Callback = OnNativeTick;
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void TimerCallback(nint timer, nint info);
    [StructLayout(LayoutKind.Sequential)]
    private struct TimerContext
    {
        internal nint Version, Info, Retain, Release, CopyDescription;
    }

    internal NativeDragClock(Action tick, Action<Exception> failed)
    {
        ArgumentNullException.ThrowIfNull(tick);
        ArgumentNullException.ThrowIfNull(failed);
        _tick = tick;
        _failed = failed;
        if (!OperatingSystem.IsMacOS())
        {
            _dispatcherTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _dispatcherTimer.Tick += OnDispatcherTick;
            _dispatcherTimer.Start();
            return;
        }

        if (CurrentLoop() != MainLoop())
            throw new InvalidOperationException("AppKit drag tracking requires its main UI thread.");
        _self = GCHandle.Alloc(this);
        try
        {
            var context = new TimerContext
            {
                Info = GCHandle.ToIntPtr(_self)
            };
            _timer = CreateTimer(0, AbsoluteTime() + 1d / 60, 1d / 60, 0, 0, Callback, ref context);
            if (_timer == 0)
                throw new InvalidOperationException("Unable to create an AppKit drag timer.");
            var library = NativeLibrary.Load(CoreFoundation);
            try
            {
                AddTimer(MainLoop(), _timer, Marshal.ReadIntPtr(NativeLibrary.GetExport(library, "kCFRunLoopCommonModes")));
            }
            finally
            {
                NativeLibrary.Free(library);
            }

            // The host may initialize its common-mode set lazily. Register the
            // actual drag mode explicitly as well; invalidation removes both.
            library = NativeLibrary.Load(AppKit);
            try
            {
                AddTimer(MainLoop(), _timer, Marshal.ReadIntPtr(NativeLibrary.GetExport(library, "NSEventTrackingRunLoopMode")));
            }
            finally
            {
                NativeLibrary.Free(library);
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private void OnDispatcherTick(object? sender, object e) => Invoke();
    private static void OnNativeTick(nint timer, nint info)
    {
        // Never allow an application exception to cross the native callback ABI.
        if (GCHandle.FromIntPtr(info).Target is NativeDragClock clock)
            clock.Invoke();
    }

    private void Invoke()
    {
        if (_disposed || _invoking)
            return;
        _invoking = true;
        try
        {
            _tick();
        }
        catch (Exception error)
        {
            Dispose();
            try
            {
                _failed(error);
            }
            catch
            { /* Native callback boundary. */
            }
        }
        finally
        {
            _invoking = false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        if (Environment.CurrentManagedThreadId != _thread)
            throw new InvalidOperationException("Dispose the drag clock on its owning UI thread.");
        _disposed = true;
        if (_dispatcherTimer is { } timer)
        {
            timer.Stop();
            timer.Tick -= OnDispatcherTick;
            _dispatcherTimer = null;
        }

        if (_timer != 0)
        {
            InvalidateTimer(_timer);
            ReleaseObject(_timer);
            _timer = 0;
        }

        if (_self.IsAllocated)
            _self.Free();
    }

    [DllImport(CoreFoundation, EntryPoint = "CFAbsoluteTimeGetCurrent")]
    private static extern double AbsoluteTime();
    [DllImport(CoreFoundation, EntryPoint = "CFRunLoopGetCurrent")]
    private static extern nint CurrentLoop();
    [DllImport(CoreFoundation, EntryPoint = "CFRunLoopGetMain")]
    private static extern nint MainLoop();
    [DllImport(CoreFoundation, EntryPoint = "CFRunLoopTimerCreate")]
    private static extern nint CreateTimer(nint allocator, double start, double interval, ulong flags, nint order, TimerCallback callback, ref TimerContext context);
    [DllImport(CoreFoundation, EntryPoint = "CFRunLoopAddTimer")]
    private static extern void AddTimer(nint loop, nint timer, nint mode);
    [DllImport(CoreFoundation, EntryPoint = "CFRunLoopTimerInvalidate")]
    private static extern void InvalidateTimer(nint timer);
    [DllImport(CoreFoundation, EntryPoint = "CFRelease")]
    private static extern void ReleaseObject(nint value);
}
