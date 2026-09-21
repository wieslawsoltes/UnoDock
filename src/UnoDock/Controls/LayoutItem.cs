using Microsoft.UI.Xaml.Data;
using Xceed.Wpf.AvalonDock.Internal;
using Xceed.Wpf.AvalonDock.Layout;

namespace Xceed.Wpf.AvalonDock.Controls;

public abstract partial class LayoutItem : FrameworkElement, IDisposable
{
    private readonly Dictionary<DependencyProperty, Binding> _bindings = [];
    private readonly Dictionary<DependencyProperty, ICommand> _commands = [];
    private DockingManager? _manager;
    private bool _disposed, _attaching;
    private readonly long _visibilityToken;
    protected LayoutItem() => _visibilityToken = RegisterPropertyChangedCallback(VisibilityProperty, (_, _) => OnVisibilityChanged());
    public LayoutContent LayoutElement { get; private set; } = null!;
    public object? Model { get; private set; }
    public ContentPresenter View { get; } = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
    internal void Attach(LayoutContent model, DockingManager manager)
    {
        LayoutElement = model; _manager = manager; Model = model.Content; DataContext = Model;
        model.PropertyChanged += ModelChanged;
        SetDefaultBindings(); InitDefaultCommands(); UpdateView();
    }
    internal void ApplyContainerStyle(Style? style)
    {
        _attaching = true;
        try { ClearDefaultBindings(); ClearDefaultCommands(); Style = style; SetDefaultBindings(); InitDefaultCommands(); }
        finally { _attaching = false; }
    }
    private bool HasStyleSetter(DependencyProperty property)
    {
        for (var style = Style; style != null; style = style.BasedOn)
            if (style.Setters.OfType<Setter>().Any(s => s.Property == property)) return true;
        return false;
    }
    protected void BindDefault(DependencyProperty property, string path)
    {
        if (ReadLocalValue(property) != DependencyProperty.UnsetValue || HasStyleSetter(property)) return;
        var binding = new Binding { Source = LayoutElement, Path = new PropertyPath(path), Mode = BindingMode.TwoWay };
        _bindings[property] = binding; SetBinding(property, binding);
    }
    protected void CommandDefault(DependencyProperty property, Action action, Func<bool> canExecute)
    {
        if (ReadLocalValue(property) != DependencyProperty.UnsetValue || HasStyleSetter(property)) return;
        var command = new DelegateCommand(_ => action(), _ => !_disposed && LayoutElement != null && canExecute());
        _commands[property] = command; SetValue(property, command);
    }
    protected virtual void SetDefaultBindings()
    {
        BindDefault(TitleProperty, nameof(LayoutContent.Title)); BindDefault(ContentIdProperty, nameof(LayoutContent.ContentId));
        BindDefault(IconSourceProperty, nameof(LayoutContent.IconSource)); BindDefault(IsActiveProperty, nameof(LayoutContent.IsActive));
        BindDefault(IsSelectedProperty, nameof(LayoutContent.IsSelected)); BindDefault(CanCloseProperty, nameof(LayoutContent.CanClose)); BindDefault(CanFloatProperty, nameof(LayoutContent.CanFloat));
    }
    protected virtual void ClearDefaultBindings()
    {
        foreach (var (property, binding) in _bindings)
            if (ReferenceEquals(GetBindingExpression(property)?.ParentBinding, binding)) ClearValue(property);
        _bindings.Clear();
    }
    protected virtual void InitDefaultCommands()
    {
        CommandDefault(ActivateCommandProperty, () => LayoutElement.IsActive = true, () => LayoutElement.IsEnabled);
        CommandDefault(CloseCommandProperty, Close, () => LayoutElement.CanClose && LayoutElement.Parent != null);
        CommandDefault(FloatCommandProperty, Float, () => LayoutElement.CanFloat && !LayoutElement.IsFloating && DockOperations.CanMove(LayoutElement));
        CommandDefault(DockAsDocumentCommandProperty, () => LayoutElement.DockAsDocument(), CanExecuteDockAsDocumentCommand);
        CommandDefault(CloseAllCommandProperty, () => CloseDocuments(false), () => Documents().Any(d => d.CanClose));
        CommandDefault(CloseAllButThisCommandProperty, () => CloseDocuments(true), () => Documents().Any(d => !ReferenceEquals(d, LayoutElement) && d.CanClose));
        CommandDefault(NewHorizontalTabGroupCommandProperty, () => Split(DockPosition.Bottom), () => CanSplit(DockPosition.Bottom));
        CommandDefault(NewVerticalTabGroupCommandProperty, () => Split(DockPosition.Right), () => CanSplit(DockPosition.Right));
        CommandDefault(MoveToNextTabGroupCommandProperty, () => Move(1), () => AdjacentPane(1) != null && DockOperations.CanMove(LayoutElement));
        CommandDefault(MoveToPreviousTabGroupCommandProperty, () => Move(-1), () => AdjacentPane(-1) != null && DockOperations.CanMove(LayoutElement));
    }
    protected virtual void ClearDefaultCommands()
    {
        foreach (var (property, command) in _commands) if (ReferenceEquals(GetValue(property), command)) ClearValue(property);
        _commands.Clear();
    }
    protected abstract void Close();
    protected virtual void Float() => LayoutElement.Float();
    protected virtual bool CanExecuteDockAsDocumentCommand() => LayoutElement.Parent is not LayoutDocumentPane && LayoutElement.Root != null;
    protected virtual void OnVisibilityChanged() { }
    protected void OnAdapterPropertyChanged(string name, DependencyPropertyChangedEventArgs args)
    {
        if (LayoutElement == null || _attaching || _disposed) return;
        switch (name)
        {
            case nameof(Title): LayoutElement.Title = Title; break;
            case nameof(ContentId): LayoutElement.ContentId = ContentId; break;
            case nameof(IconSource): LayoutElement.IconSource = IconSource; break;
            case nameof(IsActive): LayoutElement.IsActive = IsActive; break;
            case nameof(IsSelected): LayoutElement.IsSelected = IsSelected; break;
            case nameof(CanClose): LayoutElement.CanClose = CanClose; break;
            case nameof(CanFloat): LayoutElement.CanFloat = CanFloat; break;
            case "CanHide" when this is LayoutAnchorableItem tool && LayoutElement is LayoutAnchorable a: a.CanHide = tool.CanHide; break;
            case "Description" when this is LayoutDocumentItem item && LayoutElement is LayoutDocument d: d.Description = item.Description; break;
        }
        _manager?.InvalidateView();
    }
    private void ModelChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(LayoutContent.Content)) { Model = LayoutElement.Content; DataContext = Model; UpdateView(); }
        foreach (var command in _commands.Values.OfType<DelegateCommand>()) command.RaiseCanExecuteChanged();
        _manager?.InvalidateView();
    }
    internal void UpdateView()
    {
        if (_manager == null) return;
        if (!ReferenceEquals(View.Content, LayoutElement.Content))
        {
            if (LayoutElement.Content is UIElement element) VisualParenting.Detach(element);
            View.Content = LayoutElement.Content;
        }
        var template = _manager.ContentTemplate(LayoutElement, View);
        if (!ReferenceEquals(View.ContentTemplate, template)) View.ContentTemplate = template;
        View.DataContext = Model;
    }
    private IEnumerable<LayoutDocument> Documents() => (LayoutElement.Root as LayoutRoot)?.Descendents().OfType<LayoutDocument>() ?? [];
    private void CloseDocuments(bool exceptThis) { foreach (var document in Documents().Where(d => !exceptThis || !ReferenceEquals(d, LayoutElement)).ToArray()) document.Close(); }
    private bool CanSplit(DockPosition position) => LayoutElement.Parent is LayoutDocumentPane { ChildrenCount: > 1 } pane && DockOperations.CanDock(LayoutElement, pane, position);
    private void Split(DockPosition position) { if (LayoutElement.Parent is ILayoutGroup pane && CanSplit(position)) DockOperations.Dock(LayoutElement, pane, position); }
    private LayoutDocumentPane? AdjacentPane(int direction)
    {
        var panes = (LayoutElement.Root as LayoutRoot)?.Descendents().OfType<LayoutDocumentPane>().ToArray() ?? [];
        var index = Array.FindIndex(panes, p => ReferenceEquals(p, LayoutElement.Parent));
        index += direction; return index >= 0 && index < panes.Length ? panes[index] : null;
    }
    private void Move(int direction) { if (AdjacentPane(direction) is { } target) DockOperations.Dock(LayoutElement, target, DockPosition.Inside); }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        if (LayoutElement != null) LayoutElement.PropertyChanged -= ModelChanged;
        UnregisterPropertyChangedCallback(VisibilityProperty, _visibilityToken);
        ClearDefaultBindings(); ClearDefaultCommands(); VisualParenting.Detach(View); View.Content = null;
        _manager = null; Model = null; DataContext = null;
    }
}
public partial class LayoutDocumentItem : LayoutItem
{
    protected override void Close() => LayoutElement.Close();
    protected override void OnVisibilityChanged() { if (LayoutElement is LayoutDocument document) document.IsVisible = Visibility == Visibility.Visible; }
    protected override void SetDefaultBindings() { base.SetDefaultBindings(); BindDefault(DescriptionProperty, nameof(LayoutDocument.Description)); }
}
public partial class LayoutAnchorableItem : LayoutItem
{
    protected override void Close() => LayoutElement.Close();
    protected override bool CanExecuteDockAsDocumentCommand() => LayoutElement is LayoutAnchorable { CanDockAsTabbedDocument: true } && base.CanExecuteDockAsDocumentCommand();
    protected override void SetDefaultBindings() { base.SetDefaultBindings(); BindDefault(CanHideProperty, nameof(LayoutAnchorable.CanHide)); }
    protected override void ClearDefaultBindings() => base.ClearDefaultBindings();
    protected override void InitDefaultCommands()
    {
        base.InitDefaultCommands();
        CommandDefault(HideCommandProperty, () => ((LayoutAnchorable)LayoutElement).Hide(), () => LayoutElement is LayoutAnchorable { CanHide: true, IsHidden: false });
        CommandDefault(AutoHideCommandProperty, () => ((LayoutAnchorable)LayoutElement).ToggleAutoHide(), () => LayoutElement is LayoutAnchorable { CanAutoHide: true } && LayoutElement.Parent is LayoutAnchorablePane or LayoutAnchorGroup);
        CommandDefault(DockCommandProperty, () => LayoutElement.Dock(), () => LayoutElement.IsFloating || LayoutElement.Parent is LayoutDocumentPane or LayoutAnchorGroup);
    }
    protected override void ClearDefaultCommands() => base.ClearDefaultCommands();
    protected override void OnVisibilityChanged() { if (LayoutElement is LayoutAnchorable a) a.IsVisible = Visibility == Visibility.Visible; }
}
