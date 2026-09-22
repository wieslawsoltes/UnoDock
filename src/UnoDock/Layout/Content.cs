using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace Xceed.Wpf.AvalonDock.Layout;

[ContentProperty(Name = nameof(Content))]
public abstract class LayoutContent : LayoutElement, IComparable<LayoutContent>, IXmlSerializable, ILayoutPreviousContainer
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(LayoutContent), new PropertyMetadata(null, (d, e) => ((LayoutContent)d).Notify(nameof(Title))));
    public static readonly DependencyProperty ContentIdProperty = DependencyProperty.Register(nameof(ContentId), typeof(string), typeof(LayoutContent), new PropertyMetadata(null, (d, e) => ((LayoutContent)d).Notify(nameof(ContentId))));
    private object? _content, _toolTip;
    private ImageSource? _icon;
    private bool _enabled = true, _canClose = true, _canFloat = true, _active, _selected, _floating, _lastFocused, _maximized;
    private double _left, _top, _width, _height;
    private DateTime? _activated;
    private ILayoutContainer? _previous;
    private int _previousIndex = -1;
    public string? Title { get => (string?)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string? ContentId { get => (string?)GetValue(ContentIdProperty); set => SetValue(ContentIdProperty, value); }
    [System.Xml.Serialization.XmlIgnore]
    public object? Content { get => _content; set => Set(ref _content, value); }
    public object? ToolTip { get => _toolTip; set => Set(ref _toolTip, value); }
    public ImageSource? IconSource { get => _icon; set => Set(ref _icon, value); }
    public bool IsEnabled { get => _enabled; set => Set(ref _enabled, value); }
    public bool CanClose { get => _canClose; set => Set(ref _canClose, value); }
    public bool CanFloat { get => _canFloat; set => Set(ref _canFloat, value); }
    public bool IsFloating { get => _floating; internal set => Set(ref _floating, value); }
    public bool IsLastFocusedDocument { get => _lastFocused; internal set => Set(ref _lastFocused, value); }
    public bool IsMaximized { get => _maximized; set => Set(ref _maximized, value); }
    public DateTime? LastActivationTimeStamp { get => _activated; set => Set(ref _activated, value); }
    public double FloatingLeft { get => _left; set => Set(ref _left, LayoutPositionableGroup<LayoutContent>.Coordinate(value)); }
    public double FloatingTop { get => _top; set => Set(ref _top, LayoutPositionableGroup<LayoutContent>.Coordinate(value)); }
    public double FloatingWidth { get => _width; set => Set(ref _width, LayoutPositionableGroup<LayoutContent>.Dimension(value)); }
    public double FloatingHeight { get => _height; set => Set(ref _height, LayoutPositionableGroup<LayoutContent>.Dimension(value)); }
    public ILayoutContainer? PreviousContainer { get => _previous; protected set { if (Set(ref _previous, value)) PreviousContainerId = (value as LayoutElement)?.SerializationId; } }
    public string? PreviousContainerId { get; protected set; }
    [System.Xml.Serialization.XmlIgnore]
    public int PreviousContainerIndex { get => _previousIndex; set => Set(ref _previousIndex, value); }
    internal void SetPrevious(ILayoutContainer? container, int index, string? id = null)
    { PreviousContainer = container; PreviousContainerIndex = index; if (id != null) PreviousContainerId = id; }
    internal void RememberDockPosition()
    {
        if (Parent is ILayoutGroup group && !IsFloating && Parent is not LayoutAnchorGroup)
            SetPrevious(group, group.IndexOfChild(this));
    }
    [System.Xml.Serialization.XmlIgnore]
    public bool IsActive
    {
        get => _active;
        set
        {
            if (value && !IsEnabled) return;
            if (Root is LayoutRoot root)
            {
                if (value) { if (this is LayoutAnchorable { IsHidden: true } a) a.Show(); root.ActiveContent = this; }
                else if (ReferenceEquals(root.ActiveContent, this)) root.ActiveContent = null;
                else SetActive(false);
            }
            else SetActive(value);
        }
    }
    internal void SetActive(bool value)
    {
        var old = _active;
        if (!Set(ref _active, value, nameof(IsActive))) return;
        if (value) { IsSelected = true; LastActivationTimeStamp = DateTime.UtcNow; }
        OnIsActiveChanged(old, value); IsActiveChanged?.Invoke(this, EventArgs.Empty);
    }
    public bool IsSelected
    {
        get => _selected;
        set
        {
            var old = _selected;
            if (!Set(ref _selected, value)) return;
            if (value && Parent is ILayoutContentSelector selector && !ReferenceEquals(selector.SelectedContent, this))
                selector.SelectedContentIndex = selector.IndexOf(this);
            OnIsSelectedChanged(old, value); IsSelectedChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    public event EventHandler? IsActiveChanged;
    public event EventHandler? IsSelectedChanged;
    public event EventHandler<CancelEventArgs>? Closing;
    public event EventHandler? Closed;
    protected virtual void OnIsActiveChanged(bool oldValue, bool newValue) { }
    protected virtual void OnIsSelectedChanged(bool oldValue, bool newValue) { }
    protected virtual void OnClosing(CancelEventArgs args) => Closing?.Invoke(this, args);
    protected virtual void OnClosed() => Closed?.Invoke(this, EventArgs.Empty);
    protected override void OnParentChanging(ILayoutContainer? oldValue, ILayoutContainer? newValue) => base.OnParentChanging(oldValue, newValue);
    protected override void OnParentChanged(ILayoutContainer? oldValue, ILayoutContainer? newValue) => base.OnParentChanged(oldValue, newValue);
    internal virtual void RefreshPlacement() => IsFloating = this.FindParent<LayoutFloatingWindow>() != null;
    public abstract void Close();
    private bool _operationInProgress;
    protected bool TryBeginOperation() { if (_operationInProgress) return false; _operationInProgress = true; return true; }
    protected void EndOperation() => _operationInProgress = false;
    protected bool CloseCore()
    {
        if (!CanClose || Parent == null || !TryBeginOperation()) return false;
        try
        {
            var parent = Parent; var root = Root as LayoutRoot; var manager = root?.Manager;
            bool Valid() => CanClose && ReferenceEquals(Parent, parent) && ReferenceEquals(Root, root) &&
                (manager == null || ReferenceEquals(manager.Layout, root));
            var args = new CancelEventArgs(); OnClosing(args);
            if (args.Cancel || !Valid()) return false;
            if (this is LayoutDocument document && manager?.RaiseDocumentClosing(document) == true) return false;
            if (!Valid()) return false;
            using var batch = root?.BeginUpdate();
            parent.RemoveChild(this); IsSelected = false; SetActive(false);
            root?.CollectGarbage(); OnClosed();
            if (this is LayoutDocument closed) manager?.RaiseDocumentClosed(closed);
            return true;
        }
        finally { EndOperation(); }
    }

    public void Float() => DockOperations.Float(this);
    public void Dock() => InternalDock();
    protected virtual void InternalDock() => DockOperations.Restore(this);
    public void DockAsDocument() => DockOperations.AsDocument(this);
    public int CompareTo(LayoutContent? other) => other == null ? 1 : StringComparer.CurrentCultureIgnoreCase.Compare(Title, other.Title);
    public XmlSchema? GetSchema() => null;
    public virtual void ReadXml(XmlReader reader) => LayoutXml.ReadInto(this, reader);
    public virtual void WriteXml(XmlWriter writer) => LayoutXml.WriteBody(this, writer);
}

public class LayoutDocument : LayoutContent
{
    public override void ConsoleDump(int tab) => base.ConsoleDump(tab);
    private bool _canMove = true, _visible = true;
    private string? _description;
    public bool CanMove { get => _canMove; set => Set(ref _canMove, value); }
    public bool IsVisible { get => _visible; internal set => Set(ref _visible, value); }
    public string? Description { get => _description; set => Set(ref _description, value); }
    public override void Close() => CloseCore();
    protected override void InternalDock() => DockOperations.Restore(this);
    public override void ReadXml(XmlReader reader) => base.ReadXml(reader);
    public override void WriteXml(XmlWriter writer) => base.WriteXml(writer);
}

public class LayoutAnchorable : LayoutContent
{
    public override void ConsoleDump(int tab) => base.ConsoleDump(tab);
    private bool _canHide = true, _canAutoHide = true, _canDocument = true;
    private double _autoWidth, _autoHeight, _autoMinWidth = 100, _autoMinHeight = 100;
    public LayoutAnchorable() => CanClose = false;
    public bool CanHide { get => _canHide; set => Set(ref _canHide, value); }
    public bool CanAutoHide { get => _canAutoHide; set => Set(ref _canAutoHide, value); }
    public bool CanDockAsTabbedDocument { get => _canDocument; set => Set(ref _canDocument, value); }
    public double AutoHideWidth { get => _autoWidth; set => Set(ref _autoWidth, LayoutPositionableGroup<LayoutContent>.Dimension(value)); }
    public double AutoHideHeight { get => _autoHeight; set => Set(ref _autoHeight, LayoutPositionableGroup<LayoutContent>.Dimension(value)); }
    public double AutoHideMinWidth { get => _autoMinWidth; set => Set(ref _autoMinWidth, LayoutPositionableGroup<LayoutContent>.Dimension(value)); }
    public double AutoHideMinHeight { get => _autoMinHeight; set => Set(ref _autoMinHeight, LayoutPositionableGroup<LayoutContent>.Dimension(value)); }
    [System.Xml.Serialization.XmlIgnore]
    public bool IsHidden => Parent is LayoutRoot root && root.Hidden.Contains(this);
    public bool IsAutoHidden => Parent is LayoutAnchorGroup;
    [System.Xml.Serialization.XmlIgnore]
    public bool IsVisible { get => Parent != null && !IsHidden; set { if (value) Show(); else Hide(); } }
    public event EventHandler<CancelEventArgs>? Hiding;
    public event EventHandler? Hidden;
    public event EventHandler? IsVisibleChanged;
    protected virtual void OnHiding(CancelEventArgs args) => Hiding?.Invoke(this, args);
    protected virtual void OnHidden() => Hidden?.Invoke(this, EventArgs.Empty);
    public void Hide(bool cancelable = true)
    {
        if (!CanHide || IsHidden || Root is not LayoutRoot root || !TryBeginOperation()) return;
        try
        {
            var parent = Parent; var manager = root.Manager;
            var args = new CancelEventArgs(); OnHiding(args);
            if (cancelable && args.Cancel || !CanHide || !ReferenceEquals(Parent, parent) ||
                !ReferenceEquals(Root, root) || manager != null && !ReferenceEquals(manager.Layout, root)) return;
            using var batch = root.BeginUpdate();
            if (parent is ILayoutGroup group) SetPrevious(group, group.IndexOfChild(this));
            root.Hidden.Add(this); SetActive(false); OnHidden();
        }
        finally { EndOperation(); }
    }

    public void Show()
    {
        if (!IsHidden) { if (Parent != null) IsSelected = true; return; }
        DockOperations.Restore(this); IsSelected = true;
    }
    public void ToggleAutoHide() => DockOperations.ToggleAutoHide(this);
    public void AddToLayout(DockingManager manager, AnchorableShowStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(manager);
        if (Parent != null) throw new InvalidOperationException("The anchorable already belongs to a layout.");
        DockOperations.AddAnchorable(manager.Layout, this, strategy);
    }
    public override void Close() => CloseCore();
    protected override void InternalDock() => DockOperations.Restore(this);
    protected override void OnParentChanged(ILayoutContainer? oldValue, ILayoutContainer? newValue) => base.OnParentChanged(oldValue, newValue);
    internal override void RefreshPlacement()
    {
        base.RefreshPlacement(); Notify(nameof(IsHidden)); Notify(nameof(IsAutoHidden)); Notify(nameof(IsVisible));
        IsVisibleChanged?.Invoke(this, EventArgs.Empty);
    }
    public override void ReadXml(XmlReader reader) => base.ReadXml(reader);
    public override void WriteXml(XmlWriter writer) => base.WriteXml(writer);
}
