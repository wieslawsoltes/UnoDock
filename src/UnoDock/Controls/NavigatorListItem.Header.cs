using Microsoft.UI.Xaml.Data;
using UnoDock.Internal;

namespace UnoDock.Controls;

internal sealed partial class NavigatorListItem
{
    private DockHeaderPresenter? _header;
    private DependencyObject? _headerHost;
    /// <summary>Presents the row's content like its tab: the document or tool
        /// header template (data context: the LayoutContent), or the icon followed by
        /// the live title. The row's Content remains the LayoutItem it selects.</summary>
        internal void PresentHeader(LayoutItem item)
    {
        if (item.LayoutElement?.Root?.Manager is not { } manager)
        {
            ReleaseHeader();
            return;
        }

        var model = item.LayoutElement;
        _header ??= new()
        {
            IconSpacing = 6,
            VerticalAlignment = VerticalAlignment.Center
        };
        _header.Text.SetBinding(TextBlock.TextProperty, new Binding { Source = item, Path = new PropertyPath(nameof(LayoutItem.Title)) });
        _header.Update(manager, model, manager.HeaderTemplate(model, this), writeTitle: false);
        AttachHeader();
    }

    /// <summary>An application ItemTemplate presents the row instead.</summary>
    internal void ReleaseHeader()
    {
        if (_header == null)
            return;
        DetachHeader();
        _header.Text.ClearValue(TextBlock.TextProperty);
        _header = null;
        // Reapply the row template so its presenter binds the item again.
        if (Template is { } template)
        {
            Template = null;
            Template = template;
        }
    }

    private void AttachHeader()
    {
        if (_header == null)
            return;
        var host = _fluent ? _action : GetTemplateChild("ContentPresenter");
        if (host == null || ReferenceEquals(host, _headerHost))
            return;
        DetachHeader();
        switch (host)
        {
            case ContentControl control:
                control.ContentTemplate = null;
                control.Content = _header;
                break;
            case ContentPresenter presenter:
                presenter.ContentTemplate = null;
                presenter.Content = _header;
                break;
            default:
                return;
        }

        _headerHost = host;
    }

    private void DetachHeader()
    {
        switch (_headerHost)
        {
            case ContentControl control when ReferenceEquals(control.Content, _header):
                control.Content = null;
                break;
            case ContentPresenter presenter when ReferenceEquals(presenter.Content, _header):
                presenter.Content = null;
                break;
        }

        _headerHost = null;
    }
}
