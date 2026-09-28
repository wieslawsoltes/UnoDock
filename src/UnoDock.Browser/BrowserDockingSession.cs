using System.Text.Json;
using Microsoft.UI.Xaml;
using UnoDock.Layout;

namespace UnoDock.Browser;
/// <summary>Projects one browser window's leased content into a real UnoDock layout.</summary>
public sealed class BrowserDockingSession : IDisposable
{
    private readonly DockingManager _manager;
    private readonly Func<string, string> _invoke;
    private readonly IBrowserDockViewFactory _factory;
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly HashSet<string> _requestedFloats = new(StringComparer.Ordinal);
    private bool _applying;
    private bool _disposed;
    private bool _projectionDirty = true;
    private long _projectionCount;
    private string _windowId = "";
    private string _theme = "";
    public string? LastError
    {
        get;
        private set;
    }

    public BrowserDockingSession(DockingManager manager, Func<string, string> invokeJavaScript, IBrowserDockViewFactory factory)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(invokeJavaScript);
        ArgumentNullException.ThrowIfNull(factory);
        _manager = manager;
        _invoke = invokeJavaScript;
        _factory = factory;
        _manager.FloatingWindowMode = FloatingWindowMode.InSurface;
        _manager.Layout.Updated += LayoutUpdated;
        Apply(Read("ready"));
    }

    /// <summary>Call on the owning UI thread; background windows may be timer-throttled.</summary>
    public void Poll()
    {
        if (_disposed || _applying)
            return;
        try
        {
            Apply(Read("read"));
            LastError = null;
            if (!_manager.IsEnabled)
                _manager.IsEnabled = true;
        }
        catch (Exception error)
        {
            LastError = error.Message;
            _manager.IsEnabled = false;
        }
    }

    public bool Float(string contentId)
    {
        if (_disposed || !_entries.TryGetValue(contentId, out var entry) || !entry.Model.CanFloat)
            return false;
        try
        {
            using var response = Request(new()
            {
                Op = "float",
                Id = contentId,
                Lease = entry.Item.Lease
            });
            LastError = null;
            return true;
        }
        catch (Exception error)
        {
            LastError = error.Message;
            return false;
        }
    }

    private BrowserDockSnapshot Read(string operation)
    {
        using var response = Request(new()
        {
            Op = operation
        });
        var snapshot = response.RootElement.GetProperty("value").Deserialize(BrowserDockJsonContext.Default.BrowserDockSnapshot) ?? throw new InvalidOperationException("Missing browser workspace snapshot.");
        if (snapshot.Schema != 1 || snapshot.Items.Length > 200)
            throw new InvalidOperationException("Unsupported browser workspace snapshot.");
        if (_windowId.Length != 0 && snapshot.WindowId != _windowId)
            throw new InvalidOperationException("The browser window identity changed.");
        _windowId = snapshot.WindowId;
        return snapshot;
    }

    private JsonDocument Request(BrowserDockRequest request)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(BrowserDockingSession));
        var json = JsonSerializer.Serialize(request, BrowserDockJsonContext.Default.BrowserDockRequest);
        var response = JsonDocument.Parse(_invoke("window.parent.UnoDockBrowser.call(" + json + ")"));
        try
        {
            if (!response.RootElement.GetProperty("ok").GetBoolean())
                throw new InvalidOperationException(response.RootElement.GetProperty("error").GetString());
            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    private bool MatchesProjection(BrowserDockSnapshot snapshot)
    {
        if (_projectionDirty || _theme != snapshot.Theme || snapshot.Items.Length != _entries.Count)
            return false;
        if (snapshot.Active.Length > 0 && _manager.Layout.ActiveContent?.ContentId != snapshot.Active)
            return false;
        foreach (var item in snapshot.Items)
        {
            if (item.Owner != snapshot.WindowId || !_entries.TryGetValue(item.Id, out var entry) || entry.Item != item)
                return false;
        }

        return true;
    }

    private void Apply(BrowserDockSnapshot snapshot)
    {
        // Polling discovers remote edits and revoked leases; it is not a render clock.
        if (MatchesProjection(snapshot))
        {
            Report();
            return;
        }

        var incoming = snapshot.Items.ToDictionary(item => item.Id, StringComparer.Ordinal);
        if (snapshot.Items.Any(item => item.Owner != snapshot.WindowId))
            throw new InvalidOperationException("A foreign owner was present in the snapshot.");
        var refresh = _projectionDirty || _theme != snapshot.Theme;
        _projectionDirty = true;
        _applying = true;
        try
        {
            using (_manager.Layout.BeginUpdate())
            {
                foreach (var entry in _entries.Values.ToArray())
                {
                    if (!incoming.TryGetValue(entry.Item.Id, out var next) || next.Lease != entry.Item.Lease || next.Kind != entry.Item.Kind || next.Type != entry.Item.Type)
                    {
                        Remove(entry);
                        refresh = true;
                    }
                }

                foreach (var item in snapshot.Items)
                {
                    if (!_entries.TryGetValue(item.Id, out var entry))
                    {
                        var view = _factory.Create(item, (title, payload) => Publish(item.Id, item.Lease, title, payload));
                        LayoutContent model = item.Kind == "tool" ? new LayoutAnchorable() : new LayoutDocument();
                        model.ContentId = item.Id;
                        model.Title = item.Title;
                        model.Content = view.View;
                        entry = new(item, model, view);
                        _entries.Add(item.Id, entry);
                        model.Closed += ContentClosed;
                        Insert(model, item.Zone);
                        refresh = true;
                    }
                    else if (entry.Item != item)
                    {
                        // Payload changes are not docking topology changes. Updating a
                        // retained native editor must not tear down focus or selection.
                        if (entry.Model.Title != item.Title)
                            entry.Model.Title = item.Title;
                        entry.View.Update(item);
                        entry.Item = item;
                    }
                }

                if (snapshot.Active.Length > 0 && _entries.TryGetValue(snapshot.Active, out var active) && !active.Model.IsActive)
                {
                    active.Model.IsActive = true;
                    refresh = true;
                }
            }

            if (_theme != snapshot.Theme)
            {
                _theme = snapshot.Theme;
                _manager.RequestedTheme = _theme == "dark" ? ElementTheme.Dark : ElementTheme.Light;
            }

            if (refresh)
            {
                _manager.Refresh();
                _projectionCount++;
            }

            _projectionDirty = false;
            Report();
        }
        finally
        {
            _applying = false;
        }
    }

    private void Report()
    {
        var report = new BrowserDockReport
        {
            WindowId = _windowId,
            ProjectionCount = _projectionCount,
            Active = _manager.Layout.ActiveContent?.ContentId ?? "",
            Items = _entries.Values.Select(entry => entry.Item with { Title = entry.Model.Title ?? "", Payload = entry.View.Payload }).ToArray()
        };
        var json = JsonSerializer.Serialize(report, BrowserDockJsonContext.Default.BrowserDockReport);
        _invoke("window.parent.UnoDockBrowser.report(" + json + "); 'ok'");
    }

    private void Insert(LayoutContent model, string zone)
    {
        var root = _manager.Layout;
        if (model is LayoutAnchorable tool && zone != "center")
        {
            var strategy = zone switch
            {
                "left" => AnchorableShowStrategy.Left,
                "top" => AnchorableShowStrategy.Top,
                "bottom" => AnchorableShowStrategy.Bottom,
                _ => AnchorableShowStrategy.Right
            };
            DockOperations.AddAnchorable(root, tool, strategy);
        }
        else
        {
            var target = root.RootPanel.Descendents().OfType<LayoutDocumentPane>().FirstOrDefault();
            if (target == null)
            {
                target = new LayoutDocumentPane();
                root.RootPanel.Children.Add(target);
            }

            target.Children.Add(model);
            if (zone != "center" && target.ChildrenCount > 1)
            {
                var position = zone switch
                {
                    "left" => DockPosition.Left,
                    "top" => DockPosition.Top,
                    "bottom" => DockPosition.Bottom,
                    _ => DockPosition.Right
                };
                DockOperations.Dock(model, target, position);
            }
        }

        model.IsActive = true;
    }

    private void Publish(string id, long lease, string title, string payload)
    {
        if (_applying || _disposed || !_entries.TryGetValue(id, out var entry) || entry.Item.Lease != lease)
            return;
        try
        {
            var previous = entry.Item;
            using var response = Request(new()
            {
                Op = "update",
                Id = id,
                Lease = lease,
                Title = title,
                Payload = payload
            });
            var accepted = response.RootElement.GetProperty("value").Deserialize(BrowserDockJsonContext.Default.BrowserDockItem) ?? throw new InvalidOperationException("Missing browser edit acknowledgement.");
            var expected = previous with
            {
                Title = title,
                Payload = payload,
                Revision = accepted.Revision
            };
            if (accepted != expected || accepted.Revision < previous.Revision || (accepted.Revision == previous.Revision && (title != previous.Title || payload != previous.Payload)))
                throw new InvalidOperationException("The browser edit acknowledgement does not match the owned content.");
            if (_disposed || !_entries.TryGetValue(id, out var current) || !ReferenceEquals(entry, current))
                return;
            // The caller already owns this exact native text. Record the committed
            // revision before the next poll; do not echo the text into its editor.
            entry.Item = accepted;
            if (entry.Model.Title != title)
                entry.Model.Title = title;
            LastError = null;
        }
        catch (Exception error)
        {
            LastError = error.Message;
            Poll();
        }
    }

    private void ContentClosed(object? sender, EventArgs args)
    {
        if (!_applying && !_disposed && sender is LayoutContent model && model.ContentId is { } id && _entries.TryGetValue(id, out var entry) && ReferenceEquals(model, entry.Model))
        {
            try
            {
                using var response = Request(new()
                {
                    Op = "close",
                    Id = id,
                    Lease = entry.Item.Lease
                });
            }
            catch (Exception error)
            {
                LastError = error.Message;
            }
        }
    }

    private void LayoutUpdated(object? sender, EventArgs args)
    {
        if (_applying || _disposed)
            return;
        foreach (var entry in _entries.Values.ToArray())
        {
            var key = entry.Item.Id + ":" + entry.Item.Lease;
            if (!entry.Model.IsFloating)
                _requestedFloats.Remove(key);
            else if (_requestedFloats.Add(key))
                Float(entry.Item.Id);
        }
    }

    private void Remove(Entry entry)
    {
        _entries.Remove(entry.Item.Id);
        entry.Model.Closed -= ContentClosed;
        entry.Model.Parent?.RemoveChild(entry.Model);
        entry.Model.Content = null;
        entry.View.Dispose();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _manager.Layout.Updated -= LayoutUpdated;
        _applying = true;
        try
        {
            foreach (var entry in _entries.Values.ToArray())
                Remove(entry);
        }
        finally
        {
            _applying = false;
        }
    }

    private sealed class Entry(BrowserDockItem item, LayoutContent model, IBrowserDockView view)
    {
        internal BrowserDockItem Item = item;
        internal readonly LayoutContent Model = model;
        internal readonly IBrowserDockView View = view;
    }
}
