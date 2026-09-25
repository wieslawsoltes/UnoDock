using Microsoft.UI.Windowing;
using UnoDock.Layout;
using UnoDock.Internal;

namespace UnoDock.Controls;

public abstract partial class LayoutFloatingWindowControl
{
    private sealed partial class FrameResize
    {
        private LayoutFloatingWindowControl? _owner;
        private readonly List<(DependencyObject Object, DependencyProperty Property, long Token)> _tokens = [];
        private readonly List<ILayoutElement> _nodes = [];
        private bool _layoutSubscribed, _nativeSubscribed;
        private double _minimumWidth, _minimumHeight, _maximumWidth, _maximumHeight;
        private FlowDirection _flowDirection;
        private Thickness _resizeBorder;

        internal bool HasCurrentPolicy(LayoutFloatingWindowControl owner) =>
            owner.MinWidth.Equals(_minimumWidth) && owner.MinHeight.Equals(_minimumHeight) &&
            owner.MaxWidth.Equals(_maximumWidth) && owner.MaxHeight.Equals(_maximumHeight) &&
            owner.FlowDirection == _flowDirection && owner.ResizeBorderThickness.Equals(_resizeBorder);
        internal bool Revoked { get; private set; }
        internal bool IsDisposed { get; private set; }

        internal void Attach(LayoutFloatingWindowControl owner)
        {
            _owner = owner;
            _minimumWidth = owner.MinWidth; _minimumHeight = owner.MinHeight;
            _maximumWidth = owner.MaxWidth; _maximumHeight = owner.MaxHeight;
            _flowDirection = owner.FlowDirection; _resizeBorder = owner.ResizeBorderThickness;
            foreach (var property in new[] { MinWidthProperty, MinHeightProperty, MaxWidthProperty, MaxHeightProperty,
                         FlowDirectionProperty, IsEnabledProperty, IsMaximizedProperty, ResizeBorderThicknessProperty })
                Observe(owner, property);
            Observe(Manager, IsEnabledProperty);
            foreach (var node in new[] { owner.Model }.Concat(owner.Model.Descendents()).ToArray())
            { _nodes.Add(node); node.PropertyChanged += OnNodeChanged; }
            Manager.LayoutChanged += OnLayoutChanged; _layoutSubscribed = true;
            Window.AppWindow.Changed += OnNativeChanged; _nativeSubscribed = true;
        }
        private void Observe(DependencyObject target, DependencyProperty property) =>
            _tokens.Add((target, property, target.RegisterPropertyChangedCallback(property, OnPolicyChanged)));
        private void OnPolicyChanged(DependencyObject _, DependencyProperty __) => Revoke();
        private void OnLayoutChanged(object? sender, EventArgs e) => Revoke();
        private void OnNativeChanged(AppWindow sender, AppWindowChangedEventArgs e)
        {
            if (!ReferenceEquals(sender.Presenter, Presenter) || !Presenter.IsResizable ||
                Presenter.State != OverlappedPresenterState.Restored) Revoke();
        }
        private void OnNodeChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Bounds/selection/title notifications are ordinary live resize output.
            // Structural and availability changes revoke even if restored (ABA).
            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName is "Parent" or "Root" or "ChildrenCount" or
                "RootPanel" or "RootDocument" or "Content" or "IsEnabled" or "IsHidden") Revoke();
        }
        private void Revoke()
        {
            if (IsDisposed || Revoked) return;
            Revoked = true;
            // Do not restore geometry calculated under a policy/owner which has
            // just been replaced. Retire before releasing pointer capture.
            _owner?.EndFrameResize(this, false);
        }
        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true; _owner = null;
            var cleanup = new DockCleanup();
            foreach (var (target, property, token) in _tokens)
                cleanup.Attempt(() => target.UnregisterPropertyChangedCallback(property, token));
            _tokens.Clear();
            foreach (var node in _nodes) cleanup.Attempt(() => node.PropertyChanged -= OnNodeChanged);
            _nodes.Clear();
            if (_layoutSubscribed) cleanup.Attempt(() => Manager.LayoutChanged -= OnLayoutChanged);
            if (_nativeSubscribed) cleanup.Attempt(() => Window.AppWindow.Changed -= OnNativeChanged);
            _layoutSubscribed = _nativeSubscribed = false;
            cleanup.ThrowIfFailed();
        }
    }
}
