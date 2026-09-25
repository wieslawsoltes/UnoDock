using System.Runtime.ExceptionServices;

namespace UnoDock.Layout;
/// <summary>
/// Completes independent model notifications and update leases before propagating
/// observer failures. This is not a rollback of application callback side effects.
/// </summary>
internal sealed class LayoutMutation : IDisposable
{
    private readonly List<IDisposable> _updates = [];
    private List<Exception>? _failures;
    private bool _disposed;
    internal LayoutMutation(params LayoutRoot? [] roots)
    {
        var distinct = new HashSet<LayoutRoot>(ReferenceEqualityComparer.Instance);
        try
        {
            foreach (var root in roots)
            {
                if (root != null && distinct.Add(root))
                {
                    _updates.Add(root.BeginUpdate());
                }
            }
        }
        catch (Exception error)
        {
            Add(error);
            Dispose();
            throw;
        }
    }

    internal int FailureCount => _failures?.Count ?? 0;

    internal static void Execute(Action<LayoutMutation> action, params LayoutRoot? [] roots)
    {
        using var mutation = new LayoutMutation(roots);
        mutation.Run(() => action(mutation));
    }

    internal void Add(Exception error) => (_failures ??= []).Add(error);
    internal void Run(Action action)
    {
        try
        {
            action();
        }
        catch (Exception error)
        {
            Add(error);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        for (var i = _updates.Count - 1; i >= 0; i--)
        {
            Run(_updates[i].Dispose);
        }

        _updates.Clear();
        if (_failures is { Count: 1 })
        {
            ExceptionDispatchInfo.Capture(_failures[0]).Throw();
        }

        if (_failures is { Count: > 1 })
        {
            throw new AggregateException("Layout mutation observers failed.", _failures);
        }
    }
}
