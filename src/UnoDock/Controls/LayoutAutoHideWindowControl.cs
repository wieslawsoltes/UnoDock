using Microsoft.UI.Xaml.Input;
using UnoDock.Internal;
using UnoDock.Layout;

namespace UnoDock.Controls;
/// <summary>A retained managed flyout with a dedicated resize gutter and cancellable preview.</summary>
public partial class LayoutAutoHideWindowControl : ContentControl, ILayoutControl
{
    public new static readonly DependencyProperty BackgroundProperty = Control.BackgroundProperty;
    public static readonly DependencyProperty AnchorableStyleProperty = DependencyProperty.Register(nameof(AnchorableStyle), typeof(Style), typeof(LayoutAutoHideWindowControl), new PropertyMetadata(null, (d, _) => ((LayoutAutoHideWindowControl)d).ApplyAnchorableStyle()));
    private readonly Grid _root = new(), _layout = new(), _titleBar = new();
    private readonly ContentPresenter _presenter = new()
    {
        Name = "PART_AutoHideContent",
        HorizontalContentAlignment = HorizontalAlignment.Stretch,
        VerticalContentAlignment = VerticalAlignment.Stretch
    };
    private readonly TextBlock _title = new()
    {
        Margin = new(2, 0, 2, 0),
        VerticalAlignment = VerticalAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis
    };
    private readonly ContentPresenter _titleView = new()
    {
        VerticalContentAlignment = VerticalAlignment.Center
    };
    private readonly DockChromeButton _menuButton, _pinButton, _hideButton;
    [ThreadStatic]
    private static Brush? _stockCaption;
    private readonly LayoutGridResizerControl _resizer = new()
    {
        Name = "PART_AutoHideResizer"
    };
    private LayoutAnchorable? _model;
    private DockingManager? _manager;
    private Rect _viewport;
    private bool _pointerInside, _menuOpen;
    private MenuFlyout? _contextMenu, _contextSource;
    private long _openVersion;
    private double _gutter = 6;
    public LayoutAutoHideWindowControl()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        Padding = new(0);
        BorderThickness = new(0);
        MinWidth = MinHeight = 0;
        IsTabStop = false;
        for (var i = 0; i < 3; i++)
        {
            _root.ColumnDefinitions.Add(new());
            _root.RowDefinitions.Add(new());
        }

        _layout.RowDefinitions.Add(new()
        {
            Height = GridLength.Auto
        });
        _layout.RowDefinitions.Add(new()
        {
            Height = new(1, GridUnitType.Star)
        });
        _layout.BorderThickness = new(1);
        _titleBar.ColumnDefinitions.Add(new()
        {
            Width = new(1, GridUnitType.Star)
        });
        _titleBar.ColumnDefinitions.Add(new()
        {
            Width = GridLength.Auto
        });
        _titleView.Content = _title;
        _titleBar.Children.Add(_titleView);
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        _menuButton = DockChrome.Icon(DockGlyph.Menu, OpenMenu, "Auto-hidden tool menu");
        _menuButton.Name = "PART_AutoHideMenuButton";
        _titleBar.Name = "PART_AutoHideTitleBar";
        _pinButton = DockChrome.Icon(DockGlyph.Pin, Pin, "Pin tool");
        _hideButton = DockChrome.Icon(DockGlyph.Close, () =>
        {
            if (_model is { IsEnabled: true } model)
                DockVisuals.CloseOrHide(model);
        }, "Hide or close auto-hidden tool");
        if (_pinButton.Content is FrameworkElement pin)
            pin.RenderTransform = new RotateTransform
            {
                Angle = 90,
                CenterX = 5,
                CenterY = 5
            };
        foreach (var button in new[]
        {
            _menuButton,
            _pinButton,
            _hideButton
        }

        )
            button.Width = button.Height = 14;
        buttons.Children.Add(_menuButton);
        buttons.Children.Add(_pinButton);
        buttons.Children.Add(_hideButton);
        Grid.SetColumn(buttons, 1);
        _titleBar.Children.Add(buttons);
        _layout.Children.Add(_titleBar);
        Grid.SetRow(_presenter, 1);
        _layout.Children.Add(_presenter);
        _root.Children.Add(_layout);
        _root.Children.Add(_resizer);
        Content = _root;
        DockVisuals.SetName(_resizer, "Resize auto-hidden tool");
        _resizer.ResizeStarted += (_, _) => BeginResize();
        _resizer.ResizePreview += (_, delta) => PreviewResize(delta);
        _resizer.ResizeFinished += (_, canceled) => FinishResize(canceled);
        _resizer.ResizeBy += (_, delta) =>
        {
            _resizer.BeginResize();
            _resizer.UpdateResize(delta);
            _resizer.EndResize(false);
        };
        PointerEntered += (_, _) =>
        {
            _pointerInside = true;
            _manager?.Surface?.StopAutoHideTimer();
        };
        PointerExited += (_, _) =>
        {
            _pointerInside = false;
            _manager?.Surface?.StartAutoHideTimer();
        };
        GotFocus += (_, _) =>
        {
            _manager?.Surface?.StopAutoHideTimer();
            if (_model is { IsEnabled: true, IsAutoHidden: true } model && ReferenceEquals(model.Root, _manager?.Layout))
                model.IsActive = true;
        };
        LostFocus += (_, _) =>
        {
            var version = _openVersion;
            DispatcherQueue.TryEnqueue(() =>
            {
                if (version == _openVersion && _model != null && !RetainOpen)
                    _manager?.Surface?.StartAutoHideTimer();
            });
        };
        Unloaded += (_, _) => CloseView();
        IsEnabledChanged += (_, _) =>
        {
            if (!IsEnabled)
                CancelResize();
        };
        RegisterPropertyChangedCallback(FlowDirectionProperty, (_, _) => CancelResize());
    }

    public ILayoutElement Model => _model!;
    public Style? AnchorableStyle
    {
        get => (Style?)GetValue(AnchorableStyleProperty); set => SetValue(AnchorableStyleProperty, value);
    }
    internal bool RetainOpen => _pointerInside || _menuOpen || _resizer.IsDragging || HasFocusWithinCore();

    internal void Open(LayoutAnchorable model, bool activate = true)
    {
        var manager = model.Root?.Manager ?? throw new InvalidOperationException("Auto-hidden content must be attached.");
        if (!model.IsAutoHidden || !model.IsEnabled || !ReferenceEquals(model.Root, manager.Layout))
            return;
        if (!ReferenceEquals(model, _model))
        {
            var closingVersion = _openVersion;
            CloseView();
            // A host-change callback may have opened a different tool while the old
            // one was closing. The newer request owns the retained flyout.
            if (_openVersion != closingVersion + 1 || !model.IsAutoHidden || !ReferenceEquals(model.Root, manager.Layout))
                return;
            _model = model;
            _manager = manager;
            _openVersion++;
            MenuContext.SetTarget(this, model);
            model.PropertyChanged += ModelChanged;
            var openingVersion = _openVersion;
            var item = manager.GetLayoutItemFromModel(model);
            item.UpdateView();
            if (!IsCurrent(model, manager, openingVersion))
                return;
            VisualParenting.Detach(item.View);
            if (!IsCurrent(model, manager, openingVersion))
                return;
            item.View.Visibility = Visibility.Visible;
            _presenter.Content = item.View;
            ApplyAnchorableStyle();
            if (!IsCurrent(model, manager, openingVersion))
                return;
        }

        var version = _openVersion;
        UpdateChrome();
        if (!IsCurrent(model, manager, version))
            return;
        Visibility = Visibility.Visible;
        if (activate)
            model.IsActive = true;
        // Activation callbacks may replace the layout, close, pin, or select another flyout.
        if (version != _openVersion || !ReferenceEquals(model, _model))
            return;
        if (!ReferenceEquals(model.Root, manager.Layout) || !model.IsAutoHidden)
            manager.CloseAutoHide();
    }

    private void OpenMenu()
    {
        if (_model is not { IsEnabled: true } model || _manager is not { } manager)
            return;
        var version = _openVersion;
        UpdateChrome();
        if (IsCurrent(model, manager, version))
            _contextMenu?.ShowAt(_menuButton);
    }

    private void Pin()
    {
        if (_model is not { IsEnabled: true, CanAutoHide: true } model || !ReferenceEquals(model.Root, _manager?.Layout))
            return;
        var manager = _manager;
        manager?.CloseAutoHide();
        if (model.IsAutoHidden && ReferenceEquals(model.Root, manager?.Layout))
            model.ToggleAutoHide();
    }

    private void ModelChanged(object? sender, PropertyChangedEventArgs e) => ValidateResize();
    private void ApplyAnchorableStyle()
    {
        if (_model != null && _manager != null && AnchorableStyle != null)
            _manager.GetLayoutItemFromModel(_model).ApplyContainerStyle(AnchorableStyle);
    }

    private bool IsCurrent(LayoutAnchorable model, DockingManager manager, long version) => version == _openVersion && ReferenceEquals(model, _model) && ReferenceEquals(manager, _manager) && model.IsEnabled && model.IsAutoHidden && ReferenceEquals(model.Root, manager.Layout);
    internal void SetViewport(Rect viewport, Size surfaceSize)
    {
        if (_model is not { } model || _manager is not { } manager)
            return;
        var version = _openVersion;
        if (_viewport != viewport)
        {
            CancelResize();
            if (!IsCurrent(model, manager, version))
                return;
            _viewport = viewport;
        }

        UpdateChrome();
        if (!IsCurrent(model, manager, version))
            return;
        var side = model.GetSide();
        var horizontal = side is AnchorSide.Left or AnchorSide.Right;
        var available = horizontal ? viewport.Width : viewport.Height;
        var requested = horizontal ? model.AutoHideWidth : model.AutoHideHeight;
        var minimum = horizontal ? model.AutoHideMinWidth : model.AutoHideMinHeight;
        var extent = Math.Min(available, Math.Max(requested, minimum) + _gutter);
        Width = horizontal ? extent : double.NaN;
        Height = horizontal ? double.NaN : extent;
        HorizontalAlignment = side == AnchorSide.Left ? HorizontalAlignment.Left : side == AnchorSide.Right ? HorizontalAlignment.Right : HorizontalAlignment.Stretch;
        VerticalAlignment = side == AnchorSide.Top ? VerticalAlignment.Top : side == AnchorSide.Bottom ? VerticalAlignment.Bottom : VerticalAlignment.Stretch;
        Margin = new(viewport.X, viewport.Y, Math.Max(0, surfaceSize.Width - viewport.Right), Math.Max(0, surfaceSize.Height - viewport.Bottom));
    }

    internal void UpdateChrome()
    {
        if (_model is not { } model || _manager is not { } manager)
            return;
        var version = _openVersion;
        var p = DockChrome.Palette(manager);
        var side = model.GetSide();
        var horizontal = side is AnchorSide.Left or AnchorSide.Right;
        var gutter = horizontal ? manager.GridSplitterWidth : manager.GridSplitterHeight;
        gutter = double.IsFinite(gutter) && gutter > 0 ? Math.Min(64, gutter) : 6;
        if (_gutter != gutter)
        {
            CancelResize();
            if (!IsCurrent(model, manager, version))
                return;
            _gutter = gutter;
        }

        _title.Text = model.Title;
        _title.FontSize = p.FontSize;
        _title.Foreground = p.Foreground;
        // Stock public screen observations have a compact gray caption. Resource
        // overrides and explicit theme dictionaries retain ownership of colors.
        var titleHeight = manager.Resources.TryGetValue("UnoDock.AutoHideTitleHeight", out var value) && value is double h && double.IsFinite(h) ? Math.Clamp(h, 16, 96) : 16;
        _layout.RowDefinitions[0].Height = new(Math.Max(titleHeight, p.FontSize * 4 / 3));
        var titleBrush = p.Header;
        if (manager.Theme == null && manager.ActualTheme == ElementTheme.Light && !manager.Resources.TryGetValue("UnoDock.HeaderBrush", out _))
            titleBrush = _stockCaption ??= DockChrome.Color(0xf0f0f0);
        if (manager.Resources.TryGetValue("UnoDock.AutoHideTitleBrush", out var brush) && brush is Brush custom)
            titleBrush = custom;
        _root.Background = titleBrush;
        Background = _layout.Background = p.Surface;
        _titleBar.Background = model.IsActive ? p.ActiveTitle : titleBrush;
        BorderBrush = _layout.BorderBrush = p.Border;
        _menuButton.Configure(p);
        _pinButton.Configure(p);
        _hideButton.Configure(p);
        _pinButton.Visibility = model.CanAutoHide ? Visibility.Visible : Visibility.Collapsed;
        _hideButton.Visibility = model.CanHide || model.CanClose ? Visibility.Visible : Visibility.Collapsed;
        _menuButton.IsEnabled = _pinButton.IsEnabled = _hideButton.IsEnabled = model.IsEnabled;
        var template = manager.HeaderTemplate(model, _titleView, title: true);
        if (!IsCurrent(model, manager, version))
            return;
        _titleView.ContentTemplate = template;
        _titleView.Content = template == null ? _title : model;
        manager.GetLayoutItemFromModel(model).UpdateView();
        if (!IsCurrent(model, manager, version))
            return;
        _resizer.Horizontal = horizontal;
        _resizer.Background = p.Border;
        for (var i = 0; i < 3; i++)
        {
            _root.ColumnDefinitions[i].Width = i == 1 ? new(1, GridUnitType.Star) : new(horizontal && i == (side == AnchorSide.Left ? 2 : 0) ? _gutter : 0);
            _root.RowDefinitions[i].Height = i == 1 ? new(1, GridUnitType.Star) : new(!horizontal && i == (side == AnchorSide.Top ? 2 : 0) ? _gutter : 0);
        }

        Grid.SetRow(_layout, 1);
        Grid.SetColumn(_layout, 1);
        Grid.SetRow(_resizer, horizontal ? 1 : side == AnchorSide.Top ? 2 : 0);
        Grid.SetColumn(_resizer, horizontal ? side == AnchorSide.Left ? 2 : 0 : 1);
        if (_contextMenu == null || !ReferenceEquals(_contextSource, manager.AnchorableContextMenu))
        {
            DetachMenu();
            if (!IsCurrent(model, manager, version))
                return;
            _contextSource = manager.AnchorableContextMenu;
            _contextMenu = DockVisuals.Menu(manager, model);
            _contextMenu.Opening += MenuOpened;
            _contextMenu.Closed += MenuClosed;
            ContextFlyout = _contextMenu;
        }
    }

    private void MenuOpened(object? sender, object e)
    {
        _menuOpen = true;
        _manager?.Surface?.StopAutoHideTimer();
    }

    private void MenuClosed(object? sender, object e)
    {
        _menuOpen = false;
        if (!RetainOpen)
            _manager?.Surface?.StartAutoHideTimer();
    }

    private void DetachMenu()
    {
        var menu = _contextMenu;
        var wasOpen = _menuOpen;
        _menuOpen = false;
        _contextMenu = _contextSource = null;
        ContextFlyout = null;
        if (menu != null)
        {
            menu.Opening -= MenuOpened;
            menu.Closed -= MenuClosed;
            if (wasOpen)
                menu.Hide();
        }
    }

    // Flyouts are bounded by their hosting client, even if application content
    // reports a larger desired size. Clip the managed content, not native HWNDs.
    protected override Size MeasureOverride(Size constraint)
    {
        if (_model == null)
            return base.MeasureOverride(constraint);
        var bounded = new Size(Math.Min(constraint.Width, _viewport.Width), Math.Min(constraint.Height, _viewport.Height));
        var measured = base.MeasureOverride(bounded);
        return new(Math.Min(measured.Width, bounded.Width), Math.Min(measured.Height, bounded.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var bounded = _model == null ? finalSize : new Size(Math.Min(finalSize.Width, _viewport.Width), Math.Min(finalSize.Height, _viewport.Height));
        _root.Clip = new RectangleGeometry
        {
            Rect = new(0, 0, Math.Max(0, bounded.Width), Math.Max(0, bounded.Height))
        };
        return base.ArrangeOverride(bounded);
    }

    protected virtual bool HasFocusWithinCore()
    {
        if (XamlRoot == null)
            return false;
        for (var element = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject; element != null; element = VisualTreeHelper.GetParent(element))
            if (ReferenceEquals(element, this))
                return true;
        return false;
    }

    protected virtual IEnumerator LogicalChildren => (_presenter.Content is DependencyObject child ? new[]
    {
        child
    }

    : Array.Empty<DependencyObject>()).GetEnumerator();

    internal void CloseView()
    {
        var manager = _manager;
        var model = _model;
        var version = ++_openVersion;
        _model = null;
        _manager = null;
        _pointerInside = false;
        if (model != null)
            model.PropertyChanged -= ModelChanged;
        MenuContext.SetTarget(this, null);
        CancelResize();
        if (version != _openVersion)
            return;
        DetachMenu();
        if (version != _openVersion)
            return;
        _presenter.Content = null;
        _titleView.ContentTemplate = null;
        _titleView.Content = _title;
        _title.Text = "";
        ContextFlyout = null;
        Visibility = Visibility.Collapsed;
        // Publish after internal cleanup: callbacks may synchronously reopen this
        // very control, which must not then be cleared by the old close operation.
        if (version == _openVersion && ReferenceEquals(manager?.AutoHideWindow, this))
            manager.SetAutoHideHost(null);
    }
}
