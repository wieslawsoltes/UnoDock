namespace UnoDock.Layout;

public partial class LayoutRoot
{
    private bool _changingActivation;
    private bool _activationPending;
    private LayoutContent? _requestedActivation;
    private long _activationRequest;
    private bool CanActivate(LayoutContent content) => ReferenceEquals(content.Root, this) && content.IsEnabled && content is not LayoutAnchorable { IsHidden: true };
    private void ChangeActiveContent(LayoutContent? value)
    {
        if (value != null && !CanActivate(value))
        {
            throw new InvalidOperationException("Active content must be an enabled, non-hidden child of this layout.");
        }

        if (!_changingActivation && ReferenceEquals(_active, value) && (value == null || value.IsActive))
        {
            return;
        }

        // Nested requests are serialized. Even a request for the current content
        // withdraws an earlier pending request: the last explicit request wins.
        _requestedActivation = value;
        _activationPending = true;
        _activationRequest++;
        if (_changingActivation)
        {
            return;
        }

        using var mutation = new LayoutMutation(this);
        _changingActivation = true;
        try
        {
            for (var pass = 0; _activationPending; pass++)
            {
                if (pass == 64)
                {
                    throw new InvalidOperationException("Layout activation observers did not converge after 64 transitions.");
                }

                var requested = _requestedActivation;
                var request = _activationRequest;
                _activationPending = false;
                if (requested != null && !CanActivate(requested))
                {
                    requested = null;
                }

                var old = _active;
                if (ReferenceEquals(old, requested) && (requested == null || requested.IsActive))
                {
                    continue;
                }

                bool OwnsOld() => old == null || old.Root is not LayoutRoot other || ReferenceEquals(other, this) || !ReferenceEquals(other.ActiveContent, old);
                bool Current() => request == _activationRequest && ReferenceEquals(_active, old) && (requested == null || CanActivate(requested));
                var failures = mutation.FailureCount;
                if (old != null && OwnsOld())
                {
                    mutation.Run(() => old.PrepareActivation(false));
                }

                if (!Current())
                {
                    continue;
                }

                if (requested != null)
                {
                    mutation.Run(() => requested.PrepareActivation(true));
                }

                if (!Current() || failures != mutation.FailureCount)
                {
                    continue;
                }

                // No observer can see two active flags or an ActiveContent pointer
                // which disagrees with this transition's flags.
                _active = requested;
                var oldFlag = old != null && OwnsOld() ? old.CommitActivation(false) : false;
                var newFlag = requested?.CommitActivation(true) ?? false;
                if (old != null && OwnsOld())
                {
                    old.PublishActivation(oldFlag, false, mutation);
                }

                requested?.PublishActivation(newFlag, true, mutation);
                if (requested != null && CanActivate(requested) && (requested is LayoutDocument || requested.Parent is LayoutDocumentPane))
                {
                    PublishLastFocused(requested, mutation);
                }

                mutation.Run(() => Notify(nameof(ActiveContent)));
            }
        }
        catch (Exception error)
        {
            mutation.Add(error);
        }
        finally
        {
            _changingActivation = false;
            _activationPending = false;
            _requestedActivation = null;
        }
    }

    private void PublishLastFocused(LayoutContent? value, LayoutMutation mutation)
    {
        var previous = LastFocusedDocument;
        if (ReferenceEquals(previous, value))
        {
            return;
        }

        LastFocusedDocument = value;
        var ownsPrevious = previous != null && (previous.Root is not LayoutRoot other || ReferenceEquals(other, this) || !ReferenceEquals(other.LastFocusedDocument, previous));
        if (ownsPrevious)
        {
            previous!.CommitLastFocused(false);
        }

        value?.CommitLastFocused(true);
        if (ownsPrevious)
        {
            mutation.Run(() => previous!.Notify(nameof(LayoutContent.IsLastFocusedDocument)));
        }

        if (value != null && ReferenceEquals(LastFocusedDocument, value))
        {
            mutation.Run(() => value.Notify(nameof(LayoutContent.IsLastFocusedDocument)));
        }

        mutation.Run(() => Notify(nameof(LastFocusedDocument)));
    }

    private void RepairActiveContent()
    {
        if (_changingActivation)
        {
            return;
        }

        if (_active != null && !CanActivate(_active))
        {
            var next = this.Descendents().OfType<LayoutContent>().Where(CanActivate).OrderByDescending(content => content.LastActivationTimeStamp).FirstOrDefault();
            ChangeActiveContent(next);
        }

        // Detached content can be activated before insertion. Adopt that state,
        // then remove extra flags without deactivating content owned by a new root.
        var contents = this.Descendents().OfType<LayoutContent>().ToArray();
        if (_active == null)
        {
            var active = contents.FirstOrDefault(content => content.IsActive && CanActivate(content));
            if (active != null)
            {
                ChangeActiveContent(active);
            }
        }

        LayoutMutation.Execute(mutation =>
        {
            foreach (var content in contents)
            {
                if (ReferenceEquals(content.Root, this) && content.IsActive && !ReferenceEquals(_active, content))
                {
                    var old = content.CommitActivation(false);
                    content.PublishActivation(old, false, mutation);
                }
            }

            if (LastFocusedDocument != null && !ReferenceEquals(LastFocusedDocument.Root, this))
            {
                PublishLastFocused(null, mutation);
            }
        });
    }

    private void PublishElementChange(LayoutElement element, bool added)
    {
        var elements = new[]
        {
            element
        }.Concat(element.Descendents().OfType<LayoutElement>()).ToArray();
        LayoutMutation.Execute(mutation =>
        {
            foreach (var child in elements)
            {
                mutation.Run(() =>
                {
                    if (added)
                    {
                        ElementAdded?.Invoke(this, new(child));
                    }
                    else
                    {
                        ElementRemoved?.Invoke(this, new(child));
                    }
                });
            }

            mutation.Run(Invalidate);
        });
    }
}
