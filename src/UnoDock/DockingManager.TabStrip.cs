namespace UnoDock;

public partial class DockingManager
{
    public static readonly DependencyProperty DocumentTabStripModeProperty = DependencyProperty.Register(nameof(DocumentTabStripMode), typeof(DocumentTabStripMode), typeof(DockingManager), new PropertyMetadata(DocumentTabStripMode.Docking, (d, e) =>
    {
        var manager = (DockingManager)d;
        if (!Enum.IsDefined((DocumentTabStripMode)e.NewValue))
        {
            manager.SetValue(DocumentTabStripModeProperty, e.OldValue);
            throw new ArgumentOutOfRangeException(nameof(DocumentTabStripMode));
        }

        manager.InvalidateView();
    }));
    /// <summary>Presentation of document pane tabs. <see cref = "DocumentTabStripMode.TabView"/>
        /// hosts the tabs in the platform TabView (best with <c>FluentTheme</c>).</summary>
        public DocumentTabStripMode DocumentTabStripMode
    {
        get => (DocumentTabStripMode)GetValue(DocumentTabStripModeProperty);
        set => SetValue(DocumentTabStripModeProperty, value);
    }
}
