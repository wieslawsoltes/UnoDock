using System.Runtime.ExceptionServices;

namespace UnoDock.Internal;
/// <summary>Complete independent teardown steps before propagating application
/// callback failures. A single failure retains its identity and original stack;
/// multiple failures remain observable in occurrence order.</summary>
internal struct DockCleanup
{
    private List<Exception>? _failures;
    internal void Attempt(Action action)
    {
        try
        {
            action();
        }
        // Flatten only our own nested cleanup batches, in their original order.
        // An application's AggregateException remains its original exception.
        catch (CleanupFailures errors)
        {
            (_failures ??= []).AddRange(errors.InnerExceptions);
        }
        catch (Exception error)
        {
            (_failures ??= []).Add(error);
        }
    }

    internal void ThrowIfFailed()
    {
        if (_failures is not { Count: > 0 } failures)
            return;
        if (failures.Count == 1)
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        throw new CleanupFailures(failures);
    }

    private sealed class CleanupFailures(IEnumerable<Exception> failures) : AggregateException("Docking cleanup callbacks failed.", failures)
    {
    }
}
