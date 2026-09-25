namespace UnoDock.Layout;

public partial class LayoutRoot
{
    private bool _changingActive, _activePending;
    private LayoutContent? _requestedActive;
    private long _activeRequestVersion;

    private bool CanActivate(LayoutContent? value) => value == null ||
        ReferenceEquals(value.Root, this) && value.IsEnabled && value is not LayoutAnchorable { IsHidden: true };

    private void RequestActiveContent(LayoutContent? value, bool reconcile = false)
    {
        if (!CanActivate(value)) throw new InvalidOperationException("Active content must be an enabled, non-hidden child of this layout.");
        if (_changingActive)
        {
            if (!ReferenceEquals(_requestedActive, value)) _activeRequestVersion++;
            _requestedActive = value;
            _activePending = !ReferenceEquals(_active, value);
            return;
        }
        if (!reconcile && ReferenceEquals(_active, value)) return;
        _requestedActive = value;
        _activePending = true;
        _activeRequestVersion++;
        _changingActive = true;
        using var notifications = new LayoutMutationScope(this);
        try
        {
            for (var pass = 0; _activePending; pass++)
            {
                if (pass >= 64) throw new InvalidOperationException("Layout activation observers did not converge after 64 transitions.");
                value = _requestedActive;
                _activePending = false;
                if (!CanActivate(value)) value = null;
                var request = _activeRequestVersion;
                var target = value;
                var changes = new List<LayoutActiveChange>();
                var previous = _active;
                var candidates = this.Descendents().OfType<LayoutContent>().ToList();
                // A removed previous item may be detached, but it must never be
                // deactivated after a callback has transferred it to another root.
                if (previous != null && previous.Root == null && !candidates.Contains(previous)) candidates.Add(previous);
                foreach (var item in candidates)
                {
                    var desired = ReferenceEquals(item, target);
                    if (item.IsActive != desired) changes.Add(item.PrepareActiveChange(desired));
                    if (request != _activeRequestVersion) break;
                }
                if (request != _activeRequestVersion) continue;
                if (!CanActivate(target) || changes.Any(change => !change.IsPrepared))
                {
                    // A changing callback completed another operation. Reconcile
                    // the actual tree, without completing the stale transition.
                    _requestedActive = CanActivate(_active) ? _active : null;
                    _activePending = true;
                    continue;
                }

                // No callbacks in this section: readers in every subsequent event
                // observe exactly one active flag and the matching root pointer.
                foreach (var change in changes) change.Commit();
                _active = target;
                bool Current() => request == _activeRequestVersion && ReferenceEquals(_active, target) && CanActivate(target);
                foreach (var change in changes) change.Publish(Current, notifications);
                if (Current() && (target is LayoutDocument || target?.Parent is LayoutDocumentPane))
                {
                    var focused = LastFocusedDocument;
                    LastFocusedDocument = target;
                    if (focused != null && !ReferenceEquals(focused, target)) notifications.Run(() => focused.IsLastFocusedDocument = false);
                    if (Current()) notifications.Run(() => target!.IsLastFocusedDocument = true);
                    if (Current()) notifications.Run(() => Notify(nameof(LastFocusedDocument)));
                }
                if (Current()) notifications.Run(() => Notify(nameof(ActiveContent)));
            }
        }
        catch (Exception error) { notifications.Record(error); }
        finally
        {
            _changingActive = false;
            _activePending = false;
            _requestedActive = null;
        }
    }

    private void RepairActivation()
    {
        if (_changingActive) return;
        if (!CanActivate(_active))
        {
            var next = this.Descendents().OfType<LayoutContent>()
                .Where(CanActivate).OrderByDescending(c => c.LastActivationTimeStamp).FirstOrDefault();
            RequestActiveContent(next, true);
        }
        else if (this.Descendents().OfType<LayoutContent>().Any(c => c.IsActive != ReferenceEquals(c, _active)))
        {
            RequestActiveContent(_active, true);
        }
        if (LastFocusedDocument != null && !ReferenceEquals(LastFocusedDocument.Root, this))
        {
            var previous = LastFocusedDocument;
            LastFocusedDocument = null;
            using var notifications = new LayoutMutationScope();
            notifications.Run(() => previous.IsLastFocusedDocument = false);
            notifications.Run(() => Notify(nameof(LastFocusedDocument)));
        }
    }
}
