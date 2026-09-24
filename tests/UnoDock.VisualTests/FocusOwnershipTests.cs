using Microsoft.UI.Xaml.Input;
using UnoDock.Compatibility;
using UnoDock.Controls;
using UnoDock.Layout;

namespace UnoDock.Testing;

internal static class FocusOwnershipTests
{
    internal static async Task<int> Run(string output)
    {
        var tests = new TestRunner();
        foreach (var state in new[] { FocusState.Programmatic, FocusState.Keyboard, FocusState.Pointer })
        foreach (var rtl in new[] { false, true })
        {
            var label = state + (rtl ? " RTL" : " LTR");
            Add("focus ownership: queued tool event cannot replace explicitly focused document: " + label, async f =>
            {
                Check.True(f.LeftEditor.Focus(state));
                f.Document.IsActive = true;
                Check.True(f.DocumentEditor.Focus(state));
                await Task.Delay(70);
                Check.Same(f.Document, f.Host.Layout.ActiveContent);
                Check.Same(f.DocumentEditor, FocusManager.GetFocusedElement(f.Host.XamlRoot!));
                Check.Equal(0, f.Pane(f.Left).ForeignFocusNotifications);
            }, rtl);
            Add("focus ownership: final tool wins a burst of native focus requests: " + label, async f =>
            {
                Check.True(f.LeftEditor.Focus(state));
                Check.True(f.RightEditor.Focus(state));
                await Task.Delay(70);
                Check.Same(f.Right, f.Host.Layout.ActiveContent);
                Check.Same(f.RightEditor, FocusManager.GetFocusedElement(f.Host.XamlRoot!));
                Check.Equal(0, f.Pane(f.Left).ForeignFocusNotifications);
                Check.True(f.Pane(f.Right).OwnedFocusNotifications > 0);
            }, rtl);
        }
        Add("focus ownership: disabling a pending focus owner cannot reactivate its model", async f =>
        {
            Check.True(f.LeftEditor.Focus(FocusState.Programmatic));
            f.Pane(f.Left).IsEnabled = false;
            f.Document.IsActive = true; Check.True(f.DocumentEditor.Focus(FocusState.Programmatic));
            await Task.Delay(70);
            Check.Same(f.Document, f.Host.Layout.ActiveContent);
            Check.Equal(0, f.Pane(f.Left).ForeignFocusNotifications);
        });
        Add("focus ownership: replacing the layout rejects old pane focus notifications", async f =>
        {
            var oldRoot = f.Host.Layout; var pane = f.Pane(f.Left);
            Check.True(f.LeftEditor.Focus(FocusState.Programmatic));
            var replacement = new LayoutDocument { ContentId = "replacement", Title = "Replacement", Content = new TextBox { Text = "New workspace" } };
            f.Host.Layout = new() { RootPanel = new(new LayoutDocumentPane(replacement)) };
            replacement.IsActive = true; f.Host.Refresh(); f.Host.UpdateLayout();
            await Task.Delay(70);
            Check.Same(replacement, f.Host.Layout.ActiveContent);
            Check.Equal(0, pane.ForeignFocusNotifications);
            Check.True(oldRoot.Manager == null);
        });
        Add("focus ownership: legitimate tool focus still invokes the protected extension", async f =>
        {
            var pane = f.Pane(f.Left); var before = pane.OwnedFocusNotifications;
            Check.True(f.LeftEditor.Focus(FocusState.Keyboard)); await Task.Delay(70);
            Check.Same(f.Left, f.Host.Layout.ActiveContent);
            Check.True(pane.OwnedFocusNotifications > before);
            Check.Equal(0, pane.ForeignFocusNotifications);
        });
        return await tests.Run(output, "focus-ownership");

        void Add(string name, Func<Fixture, Task> body, bool rtl = false) => tests.Test(name, async () =>
        {
            using var fixture = new Fixture(rtl);
            await Wait(() => fixture.DocumentEditor.IsLoaded && fixture.LeftEditor.IsLoaded && fixture.RightEditor.IsLoaded);
            fixture.Host.UpdateLayout(); fixture.Document.IsActive = true;
            Check.True(fixture.DocumentEditor.Focus(FocusState.Programmatic)); await Task.Delay(70);
            await body(fixture);
        });
    }
    private static async Task Wait(Func<bool> predicate)
    {
        for (var i = 0; i < 100 && !predicate(); i++) await Task.Delay(20);
        Check.True(predicate(), "Focus ownership fixture did not load within its bounded wait.");
    }
    private sealed class ProbeHost : DockingManager
    {
        protected override LayoutAnchorablePaneControl CreateAnchorablePaneControl(LayoutAnchorablePane model) => new ProbePane(model);
    }
    private sealed class ProbePane(LayoutAnchorablePane model) : LayoutAnchorablePaneControl(model)
    {
        internal int ForeignFocusNotifications, OwnedFocusNotifications;
        protected override void OnGotKeyboardFocus(DockKeyboardFocusChangedEventArgs e)
        {
            var current = XamlRoot == null ? null : FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
            var owned = false;
            for (var node = current; node != null; node = VisualTreeHelper.GetParent(node))
                if (ReferenceEquals(node, this)) { owned = true; break; }
            if (owned) OwnedFocusNotifications++; else ForeignFocusNotifications++;
            base.OnGotKeyboardFocus(e);
        }
    }
    private sealed class Fixture : IDisposable
    {
        internal readonly ProbeHost Host = new() { Width = 1000, Height = 640, FloatingWindowMode = FloatingWindowMode.InSurface };
        internal readonly TextBox LeftEditor = new() { Text = "Left tool" }, RightEditor = new() { Text = "Right tool" }, DocumentEditor = new() { Text = "Document" };
        internal readonly LayoutAnchorable Left, Right;
        internal readonly LayoutDocument Document;
        private readonly Window _window;
        internal Fixture(bool rtl)
        {
            Left = new() { ContentId = "left", Title = "Left", Content = LeftEditor };
            Right = new() { ContentId = "right", Title = "Right", Content = RightEditor };
            Document = new() { ContentId = "doc", Title = "Document", Content = DocumentEditor };
            var panel = new LayoutPanel(new LayoutAnchorablePane(Left) { DockWidth = new(190) });
            panel.Children.Add(new LayoutDocumentPane(Document));
            panel.Children.Add(new LayoutAnchorablePane(Right) { DockWidth = new(190) });
            Host.FlowDirection = rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            Host.Layout = new() { RootPanel = panel }; Document.IsActive = true;
            _window = new() { Content = Host, Title = "UnoDock focus ownership acceptance" };
            _window.AppWindow.Resize(new() { Width = 1100, Height = 800 }); _window.Activate();
        }
        internal ProbePane Pane(LayoutAnchorable tool) => Host.FindVisualChildren<ProbePane>().Single(p => ReferenceEquals(p.Model, tool.Parent));
        public void Dispose() { Host.Dispose(); _window.Content = null; _window.Close(); }
    }
}
