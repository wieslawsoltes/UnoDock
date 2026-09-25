using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace UnoDock.Layout;

public class LayoutAnchorable : LayoutContent
{
    public override void ConsoleDump(int tab) => base.ConsoleDump(tab);
    private bool _canHide = true, _canAutoHide = true, _canDocument = true;
    private double _autoWidth, _autoHeight, _autoMinWidth = 100, _autoMinHeight = 100;
    public LayoutAnchorable() => CanClose = false;
    public bool CanHide
    {
        get => _canHide;
        set => Set(ref _canHide, value);
    }
    public bool CanAutoHide
    {
        get => _canAutoHide;
        set => Set(ref _canAutoHide, value);
    }
    public bool CanDockAsTabbedDocument
    {
        get => _canDocument;
        set => Set(ref _canDocument, value);
    }
    public double AutoHideWidth
    {
        get => _autoWidth;
        set => Set(ref _autoWidth, LayoutPositionableGroup<LayoutContent>.Dimension(value));
    }
    public double AutoHideHeight
    {
        get => _autoHeight;
        set => Set(ref _autoHeight, LayoutPositionableGroup<LayoutContent>.Dimension(value));
    }
    public double AutoHideMinWidth
    {
        get => _autoMinWidth;
        set => Set(ref _autoMinWidth, LayoutPositionableGroup<LayoutContent>.Dimension(value));
    }
    public double AutoHideMinHeight
    {
        get => _autoMinHeight;
        set => Set(ref _autoMinHeight, LayoutPositionableGroup<LayoutContent>.Dimension(value));
    }

    [System.Xml.Serialization.XmlIgnore]
    public bool IsHidden => Parent is LayoutRoot root && root.Hidden.Contains(this);
    public bool IsAutoHidden => Parent is LayoutAnchorGroup;

    [System.Xml.Serialization.XmlIgnore]
    public bool IsVisible
    {
        get => Parent != null && !IsHidden;
        set
        {
            if (value)
                Show();
            else
                Hide();
        }
    }

    public event EventHandler<CancelEventArgs>? Hiding;
    public event EventHandler? Hidden;
    public event EventHandler? IsVisibleChanged;
    protected virtual void OnHiding(CancelEventArgs args) => Hiding?.Invoke(this, args);
    protected virtual void OnHidden() => Hidden?.Invoke(this, EventArgs.Empty);
    public void Hide(bool cancelable = true)
    {
        if (!CanHide || IsHidden || Root is not LayoutRoot root || !TryBeginOperation())
            return;
        try
        {
            var parent = Parent;
            var manager = root.Manager;
            var args = new CancelEventArgs();
            OnHiding(args);
            if (cancelable && args.Cancel || !CanHide || !ReferenceEquals(Parent, parent) || !ReferenceEquals(Root, root) || manager != null && !ReferenceEquals(manager.Layout, root))
                return;
            using var batch = root.BeginUpdate();
            if (parent is ILayoutGroup group)
                SetPrevious(group, group.IndexOfChild(this));
            root.Hidden.Add(this);
            SetActive(false);
            OnHidden();
        }
        finally
        {
            EndOperation();
        }
    }

    public void Show()
    {
        if (!IsHidden)
        {
            if (Parent != null)
                IsSelected = true;
            return;
        }

        DockOperations.Restore(this);
        IsSelected = true;
    }

    public void ToggleAutoHide() => DockOperations.ToggleAutoHide(this);
    public void AddToLayout(DockingManager manager, AnchorableShowStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(manager);
        if (Parent != null)
            throw new InvalidOperationException("The anchorable already belongs to a layout.");
        DockOperations.AddAnchorable(manager.Layout, this, strategy);
    }

    public override void Close() => CloseCore();
    protected override void InternalDock() => DockOperations.Restore(this);
    protected override void OnParentChanged(ILayoutContainer? oldValue, ILayoutContainer? newValue) => base.OnParentChanged(oldValue, newValue);
    internal override void RefreshPlacement()
    {
        base.RefreshPlacement();
        Notify(nameof(IsHidden));
        Notify(nameof(IsAutoHidden));
        Notify(nameof(IsVisible));
        IsVisibleChanged?.Invoke(this, EventArgs.Empty);
    }

    public override void ReadXml(XmlReader reader) => base.ReadXml(reader);
    public override void WriteXml(XmlWriter writer) => base.WriteXml(writer);
}
