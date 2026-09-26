using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace UnoDock.Layout;

[ContentProperty(Name = nameof(Content))]
public abstract partial class LayoutContent : LayoutElement, IComparable<LayoutContent>, IXmlSerializable, ILayoutPreviousContainer
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
    public string? Title
    {
        get => (string?)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }
    public string? ContentId
    {
        get => (string?)GetValue(ContentIdProperty);
        set => SetValue(ContentIdProperty, value);
    }

    [System.Xml.Serialization.XmlIgnore]
    public object? Content
    {
        get => _content;
        set => Set(ref _content, value);
    }
    public object? ToolTip
    {
        get => _toolTip;
        set => Set(ref _toolTip, value);
    }
    public ImageSource? IconSource
    {
        get => _icon;
        set => Set(ref _icon, value);
    }
    public bool IsEnabled
    {
        get => _enabled;
        set => Set(ref _enabled, value);
    }
    public bool CanClose
    {
        get => _canClose;
        set => Set(ref _canClose, value);
    }
    public bool CanFloat
    {
        get => _canFloat;
        set => Set(ref _canFloat, value);
    }
    public bool IsFloating
    {
        get => _floating;
        internal set => Set(ref _floating, value);
    }
    public bool IsLastFocusedDocument
    {
        get => _lastFocused;
        internal set => Set(ref _lastFocused, value);
    }
    public bool IsMaximized
    {
        get => _maximized;
        set => Set(ref _maximized, value);
    }
    public DateTime? LastActivationTimeStamp
    {
        get => _activated;
        set => Set(ref _activated, value);
    }
    public double FloatingLeft
    {
        get => _left;
        set => Set(ref _left, LayoutPositionableGroup<LayoutContent>.Coordinate(value));
    }
    public double FloatingTop
    {
        get => _top;
        set => Set(ref _top, LayoutPositionableGroup<LayoutContent>.Coordinate(value));
    }
    public double FloatingWidth
    {
        get => _width;
        set => Set(ref _width, LayoutPositionableGroup<LayoutContent>.Dimension(value));
    }
    public double FloatingHeight
    {
        get => _height;
        set => Set(ref _height, LayoutPositionableGroup<LayoutContent>.Dimension(value));
    }

    public ILayoutContainer? PreviousContainer
    {
        get => _previous;
        protected set
        {
            if (Set(ref _previous, value))
                PreviousContainerId = (value as LayoutElement)?.SerializationId;
        }
    }

    public string? PreviousContainerId
    {
        get;
        protected set;
    }

    [System.Xml.Serialization.XmlIgnore]
    public int PreviousContainerIndex
    {
        get => _previousIndex;
        set => Set(ref _previousIndex, value);
    }

    internal void SetPrevious(ILayoutContainer? container, int index, string? id = null)
    {
        PreviousContainer = container;
        PreviousContainerIndex = index;
        if (id != null)
            PreviousContainerId = id;
    }

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
            if (value && !IsEnabled)
                return;
            if (Root is LayoutRoot root)
            {
                if (value)
                {
                    if (this is LayoutAnchorable { IsHidden: true } a)
                        a.Show();
                    root.ActiveContent = this;
                }
                else if (ReferenceEquals(root.ActiveContent, this))
                    root.ActiveContent = null;
                else
                    SetActive(false);
            }
            else
                SetActive(value);
        }
    }

    public bool IsSelected
    {
        get => _selected;
        set
        {
            var old = _selected;
            if (!Set(ref _selected, value))
                return;
            if (value && Parent is ILayoutContentSelector selector && !ReferenceEquals(selector.SelectedContent, this))
                selector.SelectedContentIndex = selector.IndexOf(this);
            OnIsSelectedChanged(old, value);
            IsSelectedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? IsActiveChanged;
    public event EventHandler? IsSelectedChanged;
    public event EventHandler<CancelEventArgs>? Closing;
    public event EventHandler? Closed;
    protected virtual void OnIsActiveChanged(bool oldValue, bool newValue)
    {
    }

    protected virtual void OnIsSelectedChanged(bool oldValue, bool newValue)
    {
    }

    protected virtual void OnClosing(CancelEventArgs args) => Closing?.Invoke(this, args);
    protected virtual void OnClosed() => Closed?.Invoke(this, EventArgs.Empty);
    protected override void OnParentChanging(ILayoutContainer? oldValue, ILayoutContainer? newValue) => base.OnParentChanging(oldValue, newValue);
    protected override void OnParentChanged(ILayoutContainer? oldValue, ILayoutContainer? newValue) => base.OnParentChanged(oldValue, newValue);
    internal virtual void RefreshPlacement() => IsFloating = this.FindParent<LayoutFloatingWindow>() != null;
    public abstract void Close();
    private bool _operationInProgress;
    protected bool TryBeginOperation()
    {
        if (_operationInProgress)
            return false;
        _operationInProgress = true;
        return true;
    }

    protected void EndOperation() => _operationInProgress = false;
    protected bool CloseCore()
    {
        if (!CanClose || Parent == null || !TryBeginOperation())
            return false;
        try
        {
            var parent = Parent;
            var root = Root as LayoutRoot;
            var manager = root?.Manager;
            bool Valid() => CanClose && ReferenceEquals(Parent, parent) && ReferenceEquals(Root, root) && (manager == null || ReferenceEquals(manager.Layout, root));
            var args = new CancelEventArgs();
            OnClosing(args);
            if (args.Cancel || !Valid())
                return false;
            if (this is LayoutDocument document && manager?.RaiseDocumentClosing(document) == true)
                return false;
            if (!Valid())
                return false;
            using var batch = root?.BeginUpdate();
            parent.RemoveChild(this);
            IsSelected = false;
            SetActive(false);
            root?.CollectGarbage();
            OnClosed();
            if (this is LayoutDocument closed)
                manager?.RaiseDocumentClosed(closed);
            return true;
        }
        finally
        {
            EndOperation();
        }
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
