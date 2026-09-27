using Microsoft.UI.Xaml.Automation;
using UnoDock.Browser;

namespace UnoDock.Gallery;

public sealed partial class BrowserTextView : UserControl, IBrowserDockView
{
    private readonly Action<string, string> _commit;
    private bool _updating = true;
    private bool _disposed;
    public FrameworkElement View => this;
    public string Payload => PayloadEditor.Text;

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
        _updating = true;
        try
        {
            if (TitleEditor.Text != item.Title)
                TitleEditor.Text = item.Title;
            if (PayloadEditor.Text != item.Payload)
                PayloadEditor.Text = item.Payload;
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
        if (!_updating && !_disposed)
            _commit(TitleEditor.Text, PayloadEditor.Text);
    }

    void IDisposable.Dispose()
    {
        _disposed = true;
        TitleEditor.TextChanged -= OnChanged;
        PayloadEditor.TextChanged -= OnChanged;
    }
}
