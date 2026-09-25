using Microsoft.UI.Xaml.Data;
using UnoDock.Internal;
using UnoDock.Layout;

namespace UnoDock.Controls;

public abstract partial class LayoutItem : FrameworkElement, IDisposable
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<LayoutRoot, BulkCloseState> BulkClosures = new();
    private sealed class BulkCloseState
    {
        internal bool Active;
    }

    private readonly Dictionary<DependencyProperty, Binding> _bindings = [];
    private readonly Dictionary<DependencyProperty, ICommand> _commands = [];
    private DockingManager? _manager;
    private bool _disposed, _attaching;
    private readonly long _visibilityToken;
    protected LayoutItem() => _visibilityToken = RegisterPropertyChangedCallback(VisibilityProperty, (_, _) => OnVisibilityChanged());
    public LayoutContent LayoutElement
    {
        get;
        private set;
    } = null!;
    public object? Model
    {
        get;
        private set;
    }

    private ContentPresenter? _view;
    private DockContextMenu? _defaultMenu;
    internal MenuFlyout GetDefaultContextMenu(DockingManager manager)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _defaultMenu ??= new DockContextMenu(this, manager);
        _defaultMenu.Refresh();
        return _defaultMenu;
    }

    internal void SuspendDefaultContextMenu() => _defaultMenu?.Suspend();
    private WeakReference<DependencyObject>? _lastFocused;
    private void RememberFocus(object sender, RoutedEventArgs e)
    {
        var focused = e.OriginalSource as DependencyObject;
        if (focused == null && _view?.XamlRoot != null)
            focused = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(_view.XamlRoot) as DependencyObject;
        if (focused != null && IsInView(focused))
            _lastFocused = new(focused);
    }

    private bool IsInView(DependencyObject element)
    {
        for (DependencyObject? current = element; current != null; current = VisualTreeHelper.GetParent(current))
            if (ReferenceEquals(current, _view))
                return true;
        return false;
    }

    internal bool RestoreEditorFocus()
    {
        if (_disposed || _view?.XamlRoot == null || !LayoutElement.IsEnabled)
            return false;
        if (_lastFocused?.TryGetTarget(out var previous) == true && IsInView(previous) && previous is Control control && control.IsEnabled && control.Visibility == Visibility.Visible && control.Focus(FocusState.Programmatic))
            return true;
        return Microsoft.UI.Xaml.Input.FocusManager.FindFirstFocusableElement(_view) is Control first && first.Focus(FocusState.Programmatic);
    }

    public bool IsViewCreated => _view != null;
    internal ContentPresenter? ExistingView => _view;

    public ContentPresenter View
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_view == null)
            {
                _view = new()
                {
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    VerticalContentAlignment = VerticalAlignment.Stretch
                };
                _view.GotFocus += RememberFocus;
                UpdateView();
            }

            return _view;
        }
    }

    internal void Attach(LayoutContent model, DockingManager manager)
    {
        LayoutElement = model;
        _manager = manager;
        Model = model.Content;
        DataContext = Model;
        model.PropertyChanged += ModelChanged;
        SetDefaultBindings();
        InitDefaultCommands();
        UpdateView();
    }

    internal void ApplyContainerStyle(Style? style)
    {
        _attaching = true;
        try
        {
            ClearDefaultBindings();
            ClearDefaultCommands();
            Style = style;
            SetDefaultBindings();
            InitDefaultCommands();
        }
        finally
        {
            _attaching = false;
        }
    }

    private bool HasStyleSetter(DependencyProperty property)
    {
        for (var style = Style; style != null; style = style.BasedOn)
            if (style.Setters.OfType<Setter>().Any(s => s.Property == property))
                return true;
        return false;
    }

    protected void BindDefault(DependencyProperty property, string path)
    {
        if (ReadLocalValue(property) != DependencyProperty.UnsetValue || HasStyleSetter(property))
            return;
        var binding = new Binding
        {
            Source = LayoutElement,
            Path = new PropertyPath(path),
            Mode = BindingMode.TwoWay
        };
        _bindings[property] = binding;
        SetBinding(property, binding);
    }

    protected void CommandDefault(DependencyProperty property, Action action, Func<bool> canExecute)
    {
        if (ReadLocalValue(property) != DependencyProperty.UnsetValue || HasStyleSetter(property))
            return;
        var command = new DelegateCommand(_ => action(), _ => !_disposed && LayoutElement != null && canExecute());
        _commands[property] = command;
        SetValue(property, command);
    }

    protected virtual void SetDefaultBindings()
    {
        BindDefault(TitleProperty, nameof(LayoutContent.Title));
        BindDefault(ContentIdProperty, nameof(LayoutContent.ContentId));
        BindDefault(IconSourceProperty, nameof(LayoutContent.IconSource));
        BindDefault(IsActiveProperty, nameof(LayoutContent.IsActive));
        BindDefault(IsSelectedProperty, nameof(LayoutContent.IsSelected));
        BindDefault(CanCloseProperty, nameof(LayoutContent.CanClose));
        BindDefault(CanFloatProperty, nameof(LayoutContent.CanFloat));
    }

    protected virtual void ClearDefaultBindings()
    {
        foreach (var (property, binding) in _bindings)
            if (ReferenceEquals(GetBindingExpression(property)?.ParentBinding, binding))
                ClearValue(property);
        _bindings.Clear();
    }

    protected virtual void InitDefaultCommands()
    {
        CommandDefault(ActivateCommandProperty, () => LayoutElement.IsActive = true, () => LayoutElement.IsEnabled);
        CommandDefault(CloseCommandProperty, Close, () => LayoutElement.CanClose && LayoutElement.Parent != null);
        CommandDefault(FloatCommandProperty, Float, () => LayoutElement.CanFloat && !LayoutElement.IsFloating && DockOperations.CanMove(LayoutElement));
        CommandDefault(DockAsDocumentCommandProperty, () => LayoutElement.DockAsDocument(), CanExecuteDockAsDocumentCommand);
        CommandDefault(CloseAllCommandProperty, () => CloseDocuments(false), () => CanCloseDocuments(false));
        CommandDefault(CloseAllButThisCommandProperty, () => CloseDocuments(true), () => CanCloseDocuments(true));
        CommandDefault(NewHorizontalTabGroupCommandProperty, () => Split(DockPosition.Bottom), () => CanSplit(DockPosition.Bottom));
        CommandDefault(NewVerticalTabGroupCommandProperty, () => Split(DockPosition.Right), () => CanSplit(DockPosition.Right));
        CommandDefault(MoveToNextTabGroupCommandProperty, () => Move(1), () => AdjacentPane(1) != null && DockOperations.CanMove(LayoutElement));
        CommandDefault(MoveToPreviousTabGroupCommandProperty, () => Move(-1), () => AdjacentPane(-1) != null && DockOperations.CanMove(LayoutElement));
    }

    protected virtual void ClearDefaultCommands()
    {
        foreach (var (property, command) in _commands)
            if (ReferenceEquals(GetValue(property), command))
                ClearValue(property);
        _commands.Clear();
    }

    protected abstract void Close();
    protected virtual void Float() => LayoutElement.Float();
    protected virtual bool CanExecuteDockAsDocumentCommand() => LayoutElement.Parent is not LayoutDocumentPane && LayoutElement.Root != null && DockOperations.CanMove(LayoutElement);
    protected virtual void OnVisibilityChanged()
    {
    }

    protected void OnAdapterPropertyChanged(string name, DependencyPropertyChangedEventArgs args)
    {
        if (LayoutElement == null || _attaching || _disposed)
            return;
        switch (name)
        {
            case nameof(Title):
                LayoutElement.Title = Title;
                break;
            case nameof(ContentId):
                LayoutElement.ContentId = ContentId;
                break;
            case nameof(IconSource):
                LayoutElement.IconSource = IconSource;
                break;
            case nameof(IsActive):
                LayoutElement.IsActive = IsActive;
                break;
            case nameof(IsSelected):
                LayoutElement.IsSelected = IsSelected;
                break;
            case nameof(CanClose):
                LayoutElement.CanClose = CanClose;
                break;
            case nameof(CanFloat):
                LayoutElement.CanFloat = CanFloat;
                break;
            case "CanHide" when this is LayoutAnchorableItem tool && LayoutElement is LayoutAnchorable a:
                a.CanHide = tool.CanHide;
                break;
            case "Description" when this is LayoutDocumentItem item && LayoutElement is LayoutDocument d:
                d.Description = item.Description;
                break;
        }

        _defaultMenu?.Refresh();
        _manager?.InvalidateView();
    }

    private void ModelChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(LayoutContent.Content))
        {
            Model = LayoutElement.Content;
            DataContext = Model;
            UpdateView();
        }

        foreach (var command in _commands.Values.OfType<DelegateCommand>().ToArray())
            command.RaiseCanExecuteChanged();
        _defaultMenu?.Refresh();
        _manager?.InvalidateView();
    }

    internal void UpdateView()
    {
        if (_manager == null || _view == null)
            return;
        if (!ReferenceEquals(View.Content, LayoutElement.Content))
        {
            if (LayoutElement.Content is UIElement element)
                VisualParenting.Detach(element);
            View.Content = LayoutElement.Content;
        }

        var template = _manager.ContentTemplate(LayoutElement, View);
        if (!ReferenceEquals(View.ContentTemplate, template))
            View.ContentTemplate = template;
        View.DataContext = Model;
    }

    private IEnumerable<LayoutDocument> Documents() => (LayoutElement.Root as LayoutRoot)?.Descendents().OfType<LayoutDocument>() ?? [];
    private bool CanCloseDocuments(bool exceptThis) => LayoutElement.Root is LayoutRoot root && !BulkClosures.GetOrCreateValue(root).Active && Documents().Any(d => d.CanClose && (!exceptThis || !ReferenceEquals(d, LayoutElement)));
    private void CloseDocuments(bool exceptThis)
    {
        if (_disposed || LayoutElement.Root is not LayoutRoot root || _manager is not { } manager || !ReferenceEquals(manager.Layout, root))
            return;
        var state = BulkClosures.GetOrCreateValue(root);
        if (state.Active)
            return;
        // Scope the operation to its original workspace, not the lifetime of the
        // initiating adapter (which may itself close first). Never close an item
        // moved by a callback into another root or enumerate replacement content.
        var snapshot = root.Descendents().OfType<LayoutDocument>().Where(d => !exceptThis || !ReferenceEquals(d, LayoutElement)).ToArray();
        state.Active = true;
        try
        {
            foreach (var document in snapshot)
            {
                if (!ReferenceEquals(manager.Layout, root) || !ReferenceEquals(root.Manager, manager))
                    break;
                if (document.CanClose && ReferenceEquals(document.Root, root) && document.Parent != null)
                    document.Close();
            }
        }
        finally
        {
            state.Active = false;
        }
    }

    private bool CanSplit(DockPosition position) => LayoutElement.Parent is LayoutDocumentPane { ChildrenCount: > 1 } pane && DockOperations.CanDock(LayoutElement, pane, position);
    private void Split(DockPosition position)
    {
        if (LayoutElement.Parent is ILayoutGroup pane && CanSplit(position))
            DockOperations.Dock(LayoutElement, pane, position);
    }

    private LayoutDocumentPane? AdjacentPane(int direction)
    {
        var panes = LayoutElement.Parent?.Parent is LayoutDocumentPaneGroup group ? group.Children.OfType<LayoutDocumentPane>().ToArray() : [];
        var index = Array.FindIndex(panes, p => ReferenceEquals(p, LayoutElement.Parent));
        index += direction;
        return index >= 0 && index < panes.Length ? panes[index] : null;
    }

    private void Move(int direction)
    {
        if (AdjacentPane(direction) is { } target)
            DockOperations.Dock(LayoutElement, target, DockPosition.Inside);
    }

#if WINDOWS
    public void Dispose()
#else
    public new void Dispose()
#endif
    {
        if (_disposed)
            return;
        _disposed = true;
        _defaultMenu?.Dispose();
        _defaultMenu = null;
        if (LayoutElement != null)
            LayoutElement.PropertyChanged -= ModelChanged;
        UnregisterPropertyChangedCallback(VisibilityProperty, _visibilityToken);
        ClearDefaultBindings();
        ClearDefaultCommands();
        if (_view is { } view)
        {
            view.GotFocus -= RememberFocus;
            _lastFocused = null;
            VisualParenting.Detach(view);
            view.Content = null;
            view.ContentTemplate = null;
            view.DataContext = null;
            _view = null;
        }

        _manager = null;
        Model = null;
        DataContext = null;
    }
}
