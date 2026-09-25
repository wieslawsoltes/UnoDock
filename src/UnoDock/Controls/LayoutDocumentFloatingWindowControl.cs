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
public class LayoutDocumentFloatingWindowControl : LayoutFloatingWindowControl
{
    private readonly LayoutDocumentFloatingWindow _model;
    public LayoutDocumentFloatingWindowControl(LayoutDocumentFloatingWindow model) : this(model, false) { }
    public LayoutDocumentFloatingWindowControl(LayoutDocumentFloatingWindow model, bool isContentImmutable) : base(model, isContentImmutable) => _model = model;
    public override ILayoutElement Model => _model;
    public LayoutItem? RootDocumentLayoutItem => !IsWindowClosed && _model.RootDocument is { } doc ? _model.Root?.Manager?.GetLayoutItemFromModel(doc) : null;
    protected override bool CanClose(object? parameter = null) => _model.RootDocument is { CanClose: true } && base.CanClose(parameter);
    protected override void OnInitialized(EventArgs e)
    {
        // Materialize the public document adapter, not its editor presenter.
        _ = RootDocumentLayoutItem;
        base.OnInitialized(e);
    }
    protected override void OnClosed(EventArgs e)
    {
        ClearValue(DataContextProperty);
        base.OnClosed(e);
    }
    protected override System.IntPtr FilterMessage(System.IntPtr hwnd, int msg, System.IntPtr wParam, System.IntPtr lParam, ref bool handled)
        => base.FilterMessage(hwnd, msg, wParam, lParam, ref handled);
}
