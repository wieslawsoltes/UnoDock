using Microsoft.UI.Xaml.Automation;
using UnoDock.Browser;

namespace UnoDock.Gallery;

public sealed partial class BrowserTextView : UserControl, IBrowserDockView
{
    private readonly Action<string, string> _commit;
    private string _title = "";
    private string _payload = "";
    private string _newLine = "\n";
    private bool _updating = true;
    private bool _disposed;
    public FrameworkElement View => this;
    public string Payload => _payload;

    public BrowserTextView(BrowserDockItem item, Action<string, string> commit)
    {
        _commit = commit;
        InitializeComponent();
        AutomationProperties.SetAutomationId(PayloadEditor, "BrowserEditor-" + item.Id);
        AutomationProperties.SetAutomationId(TitleEditor, "BrowserTitle-" + item.Id);
        Update(item);
    }

    public void Update(BrowserDockItem item)
    {
        if (_disposed)
            return;
        _updating = true;
        try
        {
            _title = item.Title;
            _payload = item.Payload;
            _newLine = _payload.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : _payload.Contains('\n') ? "\n" : _payload.Contains('\r') ? "\r" : "\n";
            if (TitleEditor.Text != _title)
                TitleEditor.Text = _title;
            if (Canonical(PayloadEditor.Text) != Canonical(_payload))
                PayloadEditor.Text = _payload;
            AutomationProperties.SetName(PayloadEditor, "Editor " + item.Id);
            AutomationProperties.SetName(TitleEditor, "Title " + item.Id);
        }
        finally
        {
            _updating = false;
        }
    }

    private void OnChanged(object sender, TextChangedEventArgs args)
    {
        if (_updating || _disposed)
            return;
        var canonical = Canonical(PayloadEditor.Text);
        var payload = canonical == Canonical(_payload) ? _payload : canonical.Replace("\n", _newLine, StringComparison.Ordinal);
        var title = TitleEditor.Text;
        // TextBox can deliver a deferred normalization event after Update returns.
        // Loading a view or editing its title must not silently rewrite its text.
        if (title == _title && payload == _payload)
            return;
        _title = title;
        _payload = payload;
        _commit(title, payload);
    }

    private static string Canonical(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    void IDisposable.Dispose()
    {
        _disposed = true;
        TitleEditor.TextChanged -= OnChanged;
        PayloadEditor.TextChanged -= OnChanged;
    }
}
