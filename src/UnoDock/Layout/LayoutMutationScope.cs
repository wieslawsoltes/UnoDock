using System.Runtime.ExceptionServices;

namespace UnoDock.Layout;

/// <summary>
/// Batches affected roots and preserves notification failures without abandoning
/// the remaining bookkeeping. This is not a rollback of application side effects.
/// </summary>
internal sealed class LayoutMutationScope : IDisposable
{
    private readonly List<IDisposable> _batches = [];
    private List<Exception>? _errors;
    private bool _disposed;

    internal LayoutMutationScope(params LayoutRoot?[] roots)
    {
        foreach (var root in roots.Distinct(ReferenceEqualityComparer.Instance))
        {
            if (root is LayoutRoot layout)
            {
                _batches.Add(layout.BeginUpdate());
            }
        }
    }

    internal void Run(Action action)
    {
        try
        {
            action();
        }
        catch (Exception error)
        {
            Record(error);
        }
    }

    internal void Record(Exception error) => (_errors ??= []).Add(error);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        for (var i = _batches.Count - 1; i >= 0; i--)
        {
            Run(_batches[i].Dispose);
        }

        if (_errors is { Count: 1 })
        {
            ExceptionDispatchInfo.Capture(_errors[0]).Throw();
        }

        if (_errors is { Count: > 1 })
        {
            throw new AggregateException("Layout mutation observers failed.", _errors);
        }
    }
}
