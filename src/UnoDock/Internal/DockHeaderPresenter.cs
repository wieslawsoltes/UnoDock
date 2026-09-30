using UnoDock.Layout;

namespace UnoDock.Internal;
/// <summary>The default [icon][title] presentation of a content in docking chrome.
/// An application header, title or menu template replaces the whole presentation
/// and receives the <see cref = "LayoutContent"/> as its data context.</summary>
internal sealed class DockHeaderPresenter : Grid
{
    internal DockHeaderPresenter(TextBlock? text = null)
    {
        Text = text ?? new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        ColumnDefinitions.Add(new()
        {
            Width = GridLength.Auto
        });
        ColumnDefinitions.Add(new()
        {
            Width = new(1, GridUnitType.Star)
        });
        Grid.SetColumn(Text, 1);
        Grid.SetColumnSpan(Templated, 2);
        Children.Add(Icon);
        Children.Add(Text);
        Children.Add(Templated);
    }

    internal ContentPresenter Icon
    {
        get;
    } = new()
    {
        Name = "PART_HeaderIcon",
        VerticalAlignment = VerticalAlignment.Center,
        Visibility = Visibility.Collapsed
    };
    internal TextBlock Text
    {
        get;
    }
    internal ContentPresenter Templated
    {
        get;
    } = new()
    {
        Name = "PART_HeaderContent",
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalContentAlignment = HorizontalAlignment.Stretch,
        VerticalContentAlignment = VerticalAlignment.Center,
        Visibility = Visibility.Collapsed
    };
    internal double IconSpacing
    {
        get;
        set;
    } = 4;
    internal bool IsTemplated => Templated.Visibility == Visibility.Visible;

    /// <summary>Title text is always written (native window titles and automation
        /// read it) unless the caller has bound it; it is shown only without a template.</summary>
        internal void Update(DockingManager manager, LayoutContent model, DataTemplate? template, string? title = null, bool writeTitle = true)
    {
        if (writeTitle)
            Text.Text = title ?? model.Title ?? "";
        Show(template, model);
        PresentIcon(manager, template == null ? model.IconSource : null);
    }

    /// <summary>Plain text without a content (for example, an empty window caption).</summary>
    internal void Reset(string title)
    {
        Text.Text = title;
        Show(null, null);
        PresentIcon(null, null);
    }

    private void Show(DataTemplate? template, LayoutContent? model)
    {
        Templated.ContentTemplate = template;
        Templated.Content = template == null ? null : model;
        Templated.Visibility = template == null ? Visibility.Collapsed : Visibility.Visible;
        Text.Visibility = template == null ? Visibility.Visible : Visibility.Collapsed;
    }

    private void PresentIcon(DockingManager? manager, object? icon)
    {
        if (manager != null)
            DockIcon.Present(Icon, manager, icon);
        else
        {
            Icon.Content = null;
            Icon.ContentTemplate = null;
            Icon.Visibility = Visibility.Collapsed;
        }

        Icon.Margin = new(0, 0, Icon.Visibility == Visibility.Visible ? IconSpacing : 0, 0);
    }
}
