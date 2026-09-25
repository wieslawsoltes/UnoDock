using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Input;
#if WINDOWS
using DockWindowActivationState = Microsoft.UI.Xaml.WindowActivationState;
#else
using DockWindowActivationState = Windows.UI.Core.CoreWindowActivationState;
#endif
using UnoDock.Internal;
using UnoDock.Layout;

namespace UnoDock.Controls;
#if WINDOWS
#else
#endif
public class LayoutAnchorableFloatingWindowControl : LayoutFloatingWindowControl
{
    public static readonly DependencyProperty SingleContentLayoutItemProperty = DependencyProperty.Register(nameof(SingleContentLayoutItem), typeof(LayoutItem), typeof(LayoutAnchorableFloatingWindowControl), new PropertyMetadata(null, (d, e) => ((LayoutAnchorableFloatingWindowControl)d).OnSingleContentLayoutItemChanged(e)));
    private readonly LayoutAnchorableFloatingWindow _model;
    public LayoutAnchorableFloatingWindowControl(LayoutAnchorableFloatingWindow model) : this(model, false)
    {
    }

    public LayoutAnchorableFloatingWindowControl(LayoutAnchorableFloatingWindow model, bool isContentImmutable) : base(model, isContentImmutable)
    {
        _model = model;
        CloseWindowCommand = new DelegateCommand(_ => Close(), parameter => CanClose(parameter));
        HideWindowCommand = new DelegateCommand(_ => Hide(), parameter => CanHide(parameter));
    }

    public override ILayoutElement Model => _model;
    public LayoutItem? SingleContentLayoutItem
    {
        get => (LayoutItem?)GetValue(SingleContentLayoutItemProperty);
        set => SetValue(SingleContentLayoutItemProperty, value);
    }
    public ICommand CloseWindowCommand
    {
        get;
        private set;
    }
    public ICommand HideWindowCommand
    {
        get;
        private set;
    }

    protected virtual void OnSingleContentLayoutItemChanged(DependencyPropertyChangedEventArgs e)
    {
    }

    protected override bool CanClose(object? parameter = null) => !IsWindowClosed && base.CanClose(parameter);
    protected override bool CanHide(object? parameter = null) => !IsWindowClosed && base.CanHide(parameter);
    protected override void DoHide()
    {
        var root = _model.Root;
        var manager = root?.Manager;
        foreach (var tool in Contents.OfType<LayoutAnchorable>().ToArray())
        {
            if (!ReferenceEquals(_model.Root, root) || manager != null && !ReferenceEquals(manager.Layout, root))
                break;
            if (ReferenceEquals(tool.FindParent<LayoutFloatingWindow>(), _model))
                tool.Hide();
        }
    }

    protected override void OnInitialized(EventArgs e)
    {
        SingleContentLayoutItem = _model.IsSinglePane && (_model.SinglePane as ILayoutContentSelector)?.SelectedContent is { } selected ? _model.Root?.Manager?.GetLayoutItemFromModel(selected) : null;
        base.OnInitialized(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        SingleContentLayoutItem = null;
        ((DelegateCommand)CloseWindowCommand).RaiseCanExecuteChanged();
        ((DelegateCommand)HideWindowCommand).RaiseCanExecuteChanged();
        base.OnClosed(e);
    }

    protected override System.IntPtr FilterMessage(System.IntPtr hwnd, int msg, System.IntPtr wParam, System.IntPtr lParam, ref bool handled) => base.FilterMessage(hwnd, msg, wParam, lParam, ref handled);
    internal override void UpdateView()
    {
        base.UpdateView();
        SingleContentLayoutItem = _model.IsSinglePane && (_model.SinglePane as ILayoutContentSelector)?.SelectedContent is { } selected ? _model.Root?.Manager?.GetLayoutItemFromModel(selected) : null;
        ((DelegateCommand)CloseWindowCommand).RaiseCanExecuteChanged();
        ((DelegateCommand)HideWindowCommand).RaiseCanExecuteChanged();
    }
}
