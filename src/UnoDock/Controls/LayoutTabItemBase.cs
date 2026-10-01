using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using UnoDock.Internal;
using UnoDock.Layout;
using UnoDock.Compatibility;

namespace UnoDock.Controls;

public abstract partial class LayoutTabItemBase : DockInputControl
{
    public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(nameof(Model), typeof(LayoutContent), typeof(LayoutTabItemBase), new PropertyMetadata(null, (d, e) => ((LayoutTabItemBase)d).OnModelChanged(e)));
    public static readonly DependencyProperty LayoutItemProperty = DependencyProperty.Register(nameof(LayoutItem), typeof(LayoutItem), typeof(LayoutTabItemBase), new PropertyMetadata(null));
    private readonly Grid _chrome = new();
    private readonly Border _selectionIndicator = new()
    {
        Name = "PART_SelectedTabIndicator",
        IsHitTestVisible = false,
        VerticalAlignment = VerticalAlignment.Top,
        Visibility = Visibility.Collapsed
    };
    private readonly DockChromeButton _label;
    private readonly DockHeaderPresenter _header = new()
    {
        VerticalAlignment = VerticalAlignment.Center,
        IconSpacing = 3
    };
    private readonly DockChromeButton _close;
    private Microsoft.UI.Xaml.Shapes.Path? _shape;
    private Canvas? _shapeHost;
    private const double SlantWidth = 12;
    private DockingManager? _manager;
    public LayoutContent? Model
    {
        get => (LayoutContent?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }
    public LayoutItem? LayoutItem => (LayoutItem?)GetValue(LayoutItemProperty);

    protected LayoutTabItemBase()
    {
        IsTabStop = false;
        Padding = new(0);
        Margin = new(0);
        Height = 20;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        _chrome.ColumnDefinitions.Add(new()
        {
            Width = new(1, GridUnitType.Star)
        });
        _chrome.ColumnDefinitions.Add(new()
        {
            Width = GridLength.Auto
        });
        _label = DockChrome.Button("", ActivateFromKeyboard);
        _label.Content = _header;
        _label.MinWidth = 44;
        _label.MaxWidth = 260;
        _label.Padding = new Thickness(1, 0, 1, 0);
        _label.HorizontalContentAlignment = HorizontalAlignment.Left;
        _label.HorizontalAlignment = HorizontalAlignment.Stretch;
        _label.VerticalAlignment = VerticalAlignment.Stretch;
        _label.DoubleTapped += (_, _) =>
        {
            if (Model?.IsFloating == true)
                DockVisuals.Dock(Model);
            else if (Model != null)
                DockVisuals.Float(Model);
        };
        _close = DockChrome.Icon(DockGlyph.Close, () =>
        {
            if (Model != null)
                DockVisuals.CloseOrHide(Model);
        }, Properties.Resources.Tab_Close);
        Grid.SetColumn(_close, 1);
        _chrome.Children.Add(_label);
        _chrome.Children.Add(_close);
        Grid.SetColumnSpan(_selectionIndicator, 2);
        _chrome.Children.Add(_selectionIndicator);
        Content = _chrome;
        InitializeTabAutomation();
    }

    protected virtual void OnModelChanged(DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is LayoutContent old)
            old.PropertyChanged -= ModelChanged;
        if (e.NewValue is LayoutContent model)
            model.PropertyChanged += ModelChanged;
        if (Model?.Root?.Manager is { } manager)
            Update(manager);
        QueueAutomationRefresh();
    }

    protected void SetLayoutItem(LayoutItem value) => SetValue(LayoutItemProperty, value);
    private void ModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_manager != null)
            Update(_manager);
        QueueAutomationRefresh();
    }

    internal override FrameworkElement DockCaptureElement => _label;

    private void ActivateFromKeyboard()
    {
        // Pointer activation occurs in the overrideable press path. A Button.Click
        // on release must not bypass a subclass that vetoed that path.
        if (_label.FocusState != FocusState.Pointer && Model is { IsEnabled: true } model)
            model.IsActive = true;
    }

    protected override bool AcceptsPointerEvent(PointerRoutedEventArgs e)
    {
        for (var current = e.OriginalSource as DependencyObject; current != null && !ReferenceEquals(current, this); current = VisualTreeHelper.GetParent(current))
            if (ReferenceEquals(current, _close))
                return false;
        return true;
    }

    protected override void OnMouseDown(DockMouseButtonEventArgs e)
    {
        if (!e.Handled && Model is { IsEnabled: true } model && e.ChangedButton == DockMouseButton.Middle)
        {
            DockVisuals.CloseOrHide(model);
            e.Handled = true;
            return;
        }

        base.OnMouseDown(e);
    }

    protected override void OnMouseLeftButtonDown(DockMouseButtonEventArgs e)
    {
        if (!e.Handled && Model is { IsEnabled: true } model)
        {
            model.IsActive = true;
            model.Root?.Manager?.BeginDrag(model, this, e.NativeEvent);
        }

        base.OnMouseLeftButtonDown(e);
    }

    protected override void OnMouseRightButtonDown(DockMouseButtonEventArgs e)
    {
        if (!e.Handled && Model is { IsEnabled: true } model)
            model.IsActive = true;
        base.OnMouseRightButtonDown(e);
    }

    private bool _pointerOver;
    protected override void OnMouseEnter(DockMouseEventArgs e)
    {
        if (!e.Handled)
            VisualStateManager.GoToState(this, "PointerOver", false);
        _pointerOver = true;
        UpdateCloseVisibility();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(DockMouseEventArgs e)
    {
        if (!e.Handled)
            VisualStateManager.GoToState(this, "Normal", false);
        _pointerOver = false;
        UpdateCloseVisibility();
        base.OnMouseLeave(e);
    }

    // A document tab offers its close button when selected or under the pointer.
    private void UpdateCloseVisibility()
    {
        if (Model == null)
            return;
        var tool = Model is LayoutAnchorable && Model.Parent is not LayoutDocumentPane;
        _close.Visibility = !tool && Model.CanClose && (Model.IsSelected || _pointerOver) ? Visibility.Visible : Visibility.Collapsed;
    }

    protected override void OnMouseMove(DockMouseEventArgs e) => base.OnMouseMove(e);
    protected override void OnMouseLeftButtonUp(DockMouseButtonEventArgs e) => base.OnMouseLeftButtonUp(e);
    internal void Update(DockingManager manager)
    {
        _manager = manager;
        if (Model == null)
            return;
        SetLayoutItem(manager.GetLayoutItemFromModel(Model));
        // A header template owns the whole header; otherwise the icon (through
        // IconContentTemplate when set) precedes the title.
        _header.Update(manager, Model, manager.HeaderTemplate(Model, this));
        _label.IsEnabled = Model.IsEnabled;
        _close.IsEnabled = Model.IsEnabled;
        var palette = DockChrome.Palette(manager);
        var tool = Model is LayoutAnchorable && Model.Parent is not LayoutDocumentPane;
        _label.Configure(palette);
        _close.Configure(palette);
        var states = palette.States;
        // Classic themes embolden the selected document tab only; tool tabs stay regular.
        var bold = Model.IsSelected && (!tool && states.BoldSelectedTab || palette.UsesFluentControls);
        _label.FontWeight = !bold ? Microsoft.UI.Text.FontWeights.Normal : states.BoldSelectedTab ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.SemiBold;
        _label.HorizontalContentAlignment = HorizontalAlignment.Left;
        _label.IsSubdued = !Model.IsSelected;
        var foreground = tool ? Model.IsSelected ? states.SelectedToolTabForeground : states.ToolTabForeground : !Model.IsSelected ? states.DocumentTabForeground : Model.IsActive ? states.ActiveDocumentTabForeground ?? states.SelectedDocumentTabForeground : states.SelectedDocumentTabForeground;
        _label.ForegroundOverride = foreground;
        _close.ForegroundOverride = foreground;
        // Hover feedback belongs to tabs that can still be selected; a selected
        // or active tab keeps its state fill under the pointer and while pressed.
        _label.HoverOverride = Model.IsSelected ? DockChrome.Transparent : states.TabHover;
        _label.PressedOverride = Model.IsSelected ? DockChrome.Transparent : null;
        _chrome.CornerRadius = tool ? new(0, 0, palette.TabCornerRadius, palette.TabCornerRadius) : new(palette.TabCornerRadius, palette.TabCornerRadius, 0, 0);
        _selectionIndicator.CornerRadius = new(palette.UsesFluentControls ? 1 : 0);
        UpdateCloseVisibility();
        var background = tool ? Model.IsSelected ? states.SelectedToolTab : states.ToolTab : !Model.IsSelected ? states.DocumentTab : Model.IsActive ? states.ActiveDocumentTab : states.SelectedDocumentTab;
        _selectionIndicator.Height = palette.ActiveTabIndicatorThickness;
        _selectionIndicator.Background = Model.IsActive ? states.ActiveTabIndicator : states.SelectedTabIndicator;
        _selectionIndicator.Margin = new Thickness(3, 0, 3, 0);
        _selectionIndicator.VerticalAlignment = states.IndicatorPlacement == DockTabIndicatorPlacement.Bottom ? VerticalAlignment.Bottom : VerticalAlignment.Top;
        _selectionIndicator.Visibility = Model.IsSelected && palette.ActiveTabIndicatorThickness > 0 ? Visibility.Visible : Visibility.Collapsed;
        var slanted = !tool && states.DocumentTabShape == DockTabShape.Slanted;
        var horizontalPadding = palette.UsesFluentControls || palette.TabHorizontalPadding > 0 ? palette.TabHorizontalPadding : tool ? 3 : 0;
        PaintShape(slanted, background, states.TabBorder, tool ? palette.ToolTabHeight - 2 : palette.TabHeight - 1);
        if (slanted)
        {
            _chrome.Background = DockChrome.Transparent;
            _chrome.BorderThickness = new(0);
            _chrome.Padding = new(horizontalPadding + SlantWidth, 0, horizontalPadding + 2, 0);
            _shapeHost!.Margin = new(-_chrome.Padding.Left, 0, -_chrome.Padding.Right, 0);
            Margin = new(Model.Parent is ILayoutGroup group && group.IndexOfChild(Model) > 0 ? -SlantWidth / 2 : 0, 0, 0, 0);
            Canvas.SetZIndex(this, Model.IsSelected ? 1 : 0);
        }
        else
        {
            _chrome.Background = background;
            _chrome.BorderBrush = states.TabBorder;
            _chrome.BorderThickness = new(0, 0, 1, 0);
            _chrome.Padding = new(horizontalPadding, 0, horizontalPadding, 0);
            Margin = new(0, 0, tool ? 0 : states.DocumentTabSpacing, 0);
            Canvas.SetZIndex(this, 0);
        }

        MinHeight = 0;
        // Some themes draw the selected document tab taller than its siblings;
        // unselected tabs then sit on the strip's baseline, lower by the raise.
        var raise = tool || Model.IsSelected ? 0 : states.SelectedTabRaise;
        Height = (tool ? palette.ToolTabHeight - 2 : palette.TabHeight - 1) - raise;
        VerticalAlignment = raise > 0 ? VerticalAlignment.Bottom : VerticalAlignment.Stretch;
        DockVisuals.SetName(_label, Model.Title ?? "Document");
        ToolTipService.SetToolTip(_label, Model.ToolTip ?? Model.Title);
        MenuContext.SetTarget(this, Model);
        ContextFlyout = DockVisuals.Menu(manager, Model);
        if (_embedded)
            ApplyEmbedded();
    }

    /// <summary>Theme-defined tab outline. The slanted outline is drawn behind
        /// the unchanged label/close layout, so hit testing and automation keep the
        /// rectangular contract of the default shape.</summary>
        private void PaintShape(bool slanted, Brush background, Brush border, double height)
    {
        if (!slanted)
        {
            if (_shapeHost != null)
                _shapeHost.Visibility = Visibility.Collapsed;
            return;
        }

        if (_shape == null)
        {
            _shape = new()
            {
                Name = "PART_TabShape",
                IsHitTestVisible = false,
                Stretch = Stretch.None,
                StrokeThickness = 1
            };
            // A Canvas reports no desired size, so the outline follows the
            // arranged tab instead of feeding its own width back into measure.
            _shapeHost = new Canvas
            {
                IsHitTestVisible = false
            };
            _shapeHost.Children.Add(_shape);
            Grid.SetColumnSpan(_shapeHost, 2);
            _chrome.Children.Insert(0, _shapeHost);
            _shapeHost.SizeChanged += (_, _) => UpdateShapeGeometry();
        }

        _shapeHost!.Visibility = Visibility.Visible;
        _shape.Visibility = Visibility.Visible;
        _shape.Fill = background;
        _shape.Stroke = border;
        _shapeHeight = height;
        UpdateShapeGeometry();
    }

    private double _shapeHeight;
    private void UpdateShapeGeometry()
    {
        if (_shape == null || _shapeHost is not { Visibility: Visibility.Visible } host)
            return;
        var width = Math.Max(SlantWidth + 6, host.ActualWidth > 0 ? host.ActualWidth : SlantWidth + 6);
        var height = Math.Max(4, host.ActualHeight > 0 ? host.ActualHeight : _shapeHeight);
        var bottom = height + .5;
        var figure = new PathFigure
        {
            StartPoint = new(.5, bottom),
            IsClosed = true,
            IsFilled = true
        };
        figure.Segments.Add(new LineSegment { Point = new(SlantWidth - 1.5, 2.5) });
        figure.Segments.Add(new BezierSegment { Point1 = new(SlantWidth - .5, .5), Point2 = new(SlantWidth + .5, .5), Point3 = new(SlantWidth + 2.5, .5) });
        figure.Segments.Add(new LineSegment { Point = new(width - 3, .5) });
        figure.Segments.Add(new BezierSegment { Point1 = new(width - 1, .5), Point2 = new(width - .5, 1), Point3 = new(width - .5, 3) });
        figure.Segments.Add(new LineSegment { Point = new(width - .5, bottom) });
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        _shape.Data = geometry;
    }

    private bool _embedded;
    /// <summary>True while a platform TabViewItem hosts this tab; the TabViewItem
        /// then supplies the background, selection, hover and close button.</summary>
        internal bool IsEmbeddedInTabView
    {
        get => _embedded;
        set
        {
            if (_embedded == value)
                return;
            _embedded = value;
            if (_manager != null)
                Update(_manager);
        }
    }

    private void ApplyEmbedded()
    {
        _chrome.Background = DockChrome.Transparent;
        _chrome.BorderThickness = new(0);
        _chrome.Padding = new(0);
        _close.Visibility = Visibility.Collapsed;
        _selectionIndicator.Visibility = Visibility.Collapsed;
        _shapeHost?.SetValue(VisibilityProperty, Visibility.Collapsed);
        _label.BackgroundOverride = null;
        _label.HoverOverride = DockChrome.Transparent;
        Margin = new(0);
        Height = double.NaN;
        VerticalAlignment = VerticalAlignment.Stretch;
        MinHeight = 0;
    }

    internal void FocusLabel() => _label.Focus(FocusState.Keyboard);
    protected override Microsoft.UI.Xaml.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new LayoutTabAutomationPeer(this);
    protected override void OnKeyDown(KeyRoutedEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || InputState.ControlDown || Model == null)
            return;
        if (e.Key is Windows.System.VirtualKey.Left or Windows.System.VirtualKey.Right or Windows.System.VirtualKey.Home or Windows.System.VirtualKey.End)
        {
            this.FindVisualAncestor<LayoutCachePaneControl>()?.NavigateHeader(Model, e.Key);
            e.Handled = true;
        }
    }

    internal void DetachModel()
    {
        MenuContext.SetTarget(this, null);
        Model = null;
        _manager = null;
        ClearValue(LayoutItemProperty);
    }
}
