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
    internal bool IsDisposed => _disposed;
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
        // The focused element itself: native WinUI may report a part of its template as the
        // event's original source.
        var focused = _view?.XamlRoot != null ? Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(_view.XamlRoot) as DependencyObject : null;
        Remember(focused ?? e.OriginalSource as DependencyObject);
    }

    // Native WinUI raises no GotFocus for an element that already has focus, so the editor is
    // also recorded when focus leaves it.
    private void RememberLeavingFocus(object sender, RoutedEventArgs e) => Remember(e.OriginalSource as DependencyObject);
    internal void RememberCurrentFocus()
    {
        if (_view?.XamlRoot is { } root && Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(root) is DependencyObject focused && IsInView(focused))
            Remember(focused);
    }

    internal void Remember(DependencyObject? element)
    {
        for (var node = element; node != null && !ReferenceEquals(node, _view); node = VisualTreeHelper.GetParent(node))
            if (node is Control { IsTabStop: true })
            {
                if (IsInView(node))
                    _lastFocused = new(node);
                return;
            }
    }

    private bool IsInView(DependencyObject element)
    {
        for (DependencyObject? current = element; current != null; current = VisualTreeHelper.GetParent(current))
            if (ReferenceEquals(current, _view))
                return true;
        return false;
    }

    /// <param name="deferred">False on the first attempt: on native WinUI, a remembered editor
    /// that cannot take focus yet is then retried on a later turn instead of falling back.</param>
    internal bool RestoreEditorFocus(bool deferred = true)
    {
        if (_disposed || _view?.XamlRoot == null || !LayoutElement.IsEnabled)
            return false;
        if (_lastFocused?.TryGetTarget(out var previous) == true && IsInView(previous) && previous is Control control && control.IsEnabled && control.Visibility == Visibility.Visible)
        {
            if (control.Focus(FocusState.Programmatic))
                return true;
#if WINDOWS
            // Native WinUI focuses only elements that took part in a layout pass, and content
            // that was just selected has not yet.
            _view.UpdateLayout();
            if (control.Focus(FocusState.Programmatic))
                return true;
            if (!deferred)
                return false;
#endif
        }

        return Microsoft.UI.Xaml.Input.FocusManager.FindFirstFocusableElement(_view) is Control first && first.Focus(FocusState.Programmatic);
    }

    /// <summary>The editor that last had focus in this item's view, if it is still there.</summary>
    internal Control? RememberedEditor => _lastFocused?.TryGetTarget(out var previous) == true && IsInView(previous) ? previous as Control : null;
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
                _view.LostFocus += RememberLeavingFocus;
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

    private bool _applyingContainerStyle, _containerStylePending;
    private Style? _requestedContainerStyle;
    private long _containerStyleRequest;
    internal void ApplyContainerStyle(Style? style)
    {
        if (_disposed)
            return;
        _requestedContainerStyle = style;
        _containerStyleRequest++;
        _containerStylePending = true;
        if (_applyingContainerStyle)
            return;
        _applyingContainerStyle = true;
        try
        {
            for (var pass = 0; _containerStylePending && !_disposed; pass++)
            {
                if (pass == 64)
                    throw new InvalidOperationException("Layout-item style callbacks did not converge.");
                _containerStylePending = false;
                var request = _containerStyleRequest;
                var requested = _requestedContainerStyle;
                bool Current() => !_disposed && request == _containerStyleRequest;
                _attaching = true;
                try
                {
                    ClearXamlBindings();
                    if (!Current())
                        continue;
                    ClearDefaultBindings();
                    if (!Current())
                        continue;
                    ClearDefaultCommands();
                    if (!Current())
                        continue;
                    Style = requested;
                    if (!Current())
                        continue;
                    SetDefaultBindings();
                    if (!Current())
                        continue;
                    InitDefaultCommands();
                }
                finally
                {
                    _attaching = false;
                }

                if (!Current())
                    continue;
                PublishLiteralStyleValues(Current);
                if (Current())
                    RefreshXamlBindings();
            }
        }
        finally
        {
            _applyingContainerStyle = false;
            _requestedContainerStyle = null;
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

    protected virtual void ClearDefaultBindings() => RetireOwnedBindings(_bindings);
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

    protected virtual void ClearDefaultCommands() => RetireOwnedCommands();
    protected abstract void Close();
    protected virtual void Float() => LayoutElement.Float();
    protected virtual bool CanExecuteDockAsDocumentCommand() => LayoutElement.Parent is not LayoutDocumentPane && LayoutElement.Root != null && DockOperations.CanMove(LayoutElement);
    protected virtual void OnVisibilityChanged()
    {
    }

    /// <summary>Model visibility flows back to the item (two-way), so bindings
        /// to the item's Visibility observe Hide/Show from the chrome.</summary>
        private protected virtual void SyncVisibilityFromModel()
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
            case "CanMove" when this is LayoutDocumentItem documentItem && LayoutElement is LayoutDocument movable:
                movable.CanMove = documentItem.CanMove;
                break;
            case "CanAutoHide" when this is LayoutAnchorableItem autoHideItem && LayoutElement is LayoutAnchorable autoHideTool:
                autoHideTool.CanAutoHide = autoHideItem.CanAutoHide;
                break;
            case "CanDockAsTabbedDocument" when this is LayoutAnchorableItem dockItem && LayoutElement is LayoutAnchorable dockTool:
                dockTool.CanDockAsTabbedDocument = dockItem.CanDockAsTabbedDocument;
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
        // Unsubscription does not revoke an already captured multicast delivery.
        if (_disposed)
            return;
#if WINDOWS
        // Native WinUI raises no focus events for some programmatic focus moves; the editor
        // is recorded when this content stops being active.
        if (args.PropertyName == nameof(LayoutContent.IsActive) && !LayoutElement.IsActive)
            RememberCurrentFocus();
#endif
        if (args.PropertyName == nameof(LayoutContent.Content))
        {
            Model = LayoutElement.Content;
            DataContext = Model;
            UpdateView();
        }

        var cleanup = new DockCleanup();
        if (args.PropertyName is "IsVisible" or "IsHidden" or null or "")
            cleanup.Attempt(SyncVisibilityFromModel);
        cleanup.Attempt(() => SynchronizeXamlModelValue(args.PropertyName));
        foreach (var command in _commands.Values.OfType<DelegateCommand>().ToArray())
            cleanup.Attempt(command.RaiseCanExecuteChanged);
        cleanup.Attempt(() => _defaultMenu?.Refresh());
        cleanup.Attempt(() => _manager?.InvalidateView());
        cleanup.ThrowIfFailed();
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
            VisualParenting.Hosted(View, LayoutElement.Content);
        }

        var template = _manager.ContentTemplate(LayoutElement, View);
        if (!ReferenceEquals(View.ContentTemplate, template))
            View.ContentTemplate = template;
        View.DataContext = Model;
#if WINDOWS
        // An editor that already had focus when this item was created raises no focus event.
        if (_lastFocused == null && View.XamlRoot is { } root && Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(root) is DependencyObject focused && IsInView(focused))
            Remember(focused);
#endif
    }

    private IEnumerable<LayoutDocument> Documents() => (LayoutElement.Root as LayoutRoot)?.Descendents().OfType<LayoutDocument>() ?? [];
    /// <summary>Run a chrome action through the item's (possibly application
        /// supplied) command, honoring its CanExecute. Without a layout item (content
        /// outside a manager) the model fallback runs instead.</summary>
        internal static void Execute(LayoutContent content, Func<LayoutItem, ICommand?> command, Action fallback)
    {
        if (content.Root is not LayoutRoot { Manager: { } manager } || manager.GetLayoutItemFromModel(content) is not { } item)
        {
            fallback();
            return;
        }

        if (command(item) is { } action && action.CanExecute(null))
            action.Execute(null);
    }

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
                    Execute(document, item => item.CloseCommand, document.Close);
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
            DockOperations.Dock(LayoutElement, pane, position, -1, asDocument: pane is LayoutDocumentPane);
    }

    // Only an immediate sibling document pane in the same group is a target; a
    // nested group in between is not skipped.
    private LayoutDocumentPane? AdjacentPane(int direction)
    {
        if (LayoutElement.Parent is not LayoutDocumentPane pane || pane.Parent is not LayoutDocumentPaneGroup group)
            return null;
        var index = group.IndexOfChild(pane) + direction;
        return index >= 0 && index < group.ChildrenCount ? group.Children[index] as LayoutDocumentPane : null;
    }

    private void Move(int direction)
    {
        if (AdjacentPane(direction) is { } target)
            DockOperations.Dock(LayoutElement, target, DockPosition.Inside, 0, asDocument: true);
    }

#if WINDOWS
    public void Dispose()
#else
    public new void Dispose()
#endif
    {
        DisposeItemCore();
    }
}
