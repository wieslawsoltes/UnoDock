using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace UnoDock.Internal;

internal sealed partial class DockChromeButton
{
    private ContentPresenter? _iconPresenter;
    private long _iconForegroundToken;
    protected override void OnApplyTemplate()
    {
        if (_iconPresenter is { } previous)
            previous.UnregisterPropertyChangedCallback(ContentPresenter.ForegroundProperty, _iconForegroundToken);
        _iconPresenter = null;
        base.OnApplyTemplate();
        // Native Button states change the presenter's Foreground, not necessarily
        // the Button.Foreground property. Vector paths do not inherit Foreground.
        // Observe that one native state value instead of introducing a second
        // hover/pressed/disabled state machine for the icon.
        if (_fluent && GetTemplateChild("ContentPresenter") is ContentPresenter presenter)
        {
            _iconPresenter = presenter;
            _iconForegroundToken = presenter.RegisterPropertyChangedCallback(ContentPresenter.ForegroundProperty, (_, _) => PaintIcon());
        }

        Paint();
    }

    private void PaintIcon()
    {
        if (Content is not Path path)
            return;
        var foreground = _fluent && _iconPresenter != null ? _iconPresenter.Foreground : Foreground;
        path.Stroke = foreground;
        path.Fill = foreground;
    }
}
