using System.Windows.Input;
using UnoDock.Controls;
using UnoDock.Themes;

namespace UnoDock.Gallery;

public sealed partial class GalleryPage
{
    private void ShowMenuLab()
    {
        var manager = new GalleryDockingManager { MinHeight = 370, RequestedTheme = ElementTheme.Light,
            FloatingWindowMode = FloatingWindowMode.InSurface };
        var panel = new Grid(); panel.RowDefinitions.Add(new() { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new() { Height = GridLength.Auto }); panel.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new(6) };
        var status = new TextBlock { Margin = new(10, 4, 10, 8), TextWrapping = TextWrapping.Wrap };
        LayoutDocument editor = null!; LayoutAnchorable explorer = null!;
        ICommand? originalFloat = null; var cancelled = false; var overridden = false; var queryEnabled = true;
        var overrideFloat = new MenuLabCommand(() => { status.Text = "Application Float command executed; model intentionally unchanged."; }, () => queryEnabled);
        Add("Dropdown contracts", ShowDropDownLab); Add("Reset", Populate); Add("Document menu", () => Show(editor)); Add("Tool menu", () => Show(explorer));
        Add("CanClose", () => { editor.CanClose = !editor.CanClose; Status(); });
        Add("CanFloat", () => { editor.CanFloat = !editor.CanFloat; Status(); });
        Add("CanHide", () => { explorer.CanHide = !explorer.CanHide; Status(); });
        Add("CanAutoHide", () => { explorer.CanAutoHide = !explorer.CanAutoHide; Status(); });
        Add("CanDockAsDocument", () => { explorer.CanDockAsTabbedDocument = !explorer.CanDockAsTabbedDocument; Status(); });
        Add("Cancel close", () => { cancelled = !cancelled; Status(); });
        Add("Override Float", () =>
        {
            if (!ReferenceEquals(editor.Root, manager.Layout)) { status.Text = "Reset the closed document first."; return; }
            overridden = !overridden; manager.GetLayoutItemFromModel(editor).FloatCommand = overridden ? overrideFloat : originalFloat; Status();
        });
        Add("Command enabled", () => { queryEnabled = !queryEnabled; overrideFloat.Raise(); Status(); });
        Add("RTL / LTR", () => manager.FlowDirection = manager.FlowDirection == FlowDirection.RightToLeft ? FlowDirection.LeftToRight : FlowDirection.RightToLeft);
        Add("Light / dark", () => { manager.RequestedTheme = manager.RequestedTheme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark; manager.Theme = new FluentTheme(manager.RequestedTheme); });
        Add("Large / stock", () => { if (manager.Resources.ContainsKey("UnoDock.FontSize")) manager.Resources.Remove("UnoDock.FontSize"); else manager.Resources["UnoDock.FontSize"] = 22d; manager.Refresh(); });
        panel.Children.Add(new ScrollViewer { Content = actions, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled });
        Grid.SetRow(status, 1); panel.Children.Add(status); Grid.SetRow(manager, 2); panel.Children.Add(manager); Populate();
        var document = new LayoutDocument { Title = "Menu quality", ContentId = "menu-lab:" + Guid.NewGuid().ToString("N"), Content = panel };
        var ownerRoot = Dock.Layout; EventHandler? changed = null;
        changed = (_, _) => { if (!ReferenceEquals(Dock.Layout, ownerRoot)) { manager.Dispose(); Dock.LayoutChanged -= changed; } };
        Dock.LayoutChanged += changed; document.Closed += (_, _) => { Dock.LayoutChanged -= changed; manager.Dispose(); };
        var pane = Dock.Layout.Descendents().OfType<LayoutDocumentPane>().FirstOrDefault();
        if (pane == null) { pane = new(); Dock.Layout.RootPanel.Children.Add(pane); }
        pane.Children.Add(document); document.IsActive = true;
        void Populate()
        {
            overridden = false; editor = new LayoutDocument { Title = "Workspace.cs", ContentId = "menu-editor",
                Content = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Padding = new(14),
                    Text = "Right-click a document tab or use Document menu.\n\nThe stock menu hides unavailable group actions and retains disabled Float/Dock commands. Tool menus have a separate action set; an auto-hidden tool offers Restore.\n\nToggle capabilities, close cancellation or the application command override. Default rows remain native menu items with keyboard and Invoke automation support." } };
            editor.Closing += (_, e) => { e.Cancel = cancelled; if (cancelled) status.Text = "Closing was cancelled by application policy."; };
            explorer = new LayoutAnchorable { Title = "Solution Explorer", ContentId = "menu-tool", Content = new TextBlock { Text = "Tool menu\n\nFloat • Dock\nAuto Hide / Restore\nHide", Margin = new(14) } };
            var docs = new LayoutDocumentPane(editor); docs.Children.Add(new LayoutDocument { Title = "Readme.md", ContentId = "menu-readme", Content = new TextBox { AcceptsReturn = true, Text = "A second document enables tab-group actions." } });
            var root = new LayoutPanel(new LayoutAnchorablePane(explorer) { DockWidth = new(220) }); root.Children.Add(docs);
            manager.Layout = new() { RootPanel = root }; editor.IsActive = true;
            originalFloat = manager.GetLayoutItemFromModel(editor).FloatCommand; manager.Refresh(); Status();
        }
        void Show(LayoutContent model)
        {
            if (!ReferenceEquals(model.Root, manager.Layout)) { status.Text = "Reset the closed document/tool first."; return; }
            manager.Refresh(); manager.UpdateLayout();
            FrameworkElement? target = model is LayoutAnchorable { IsAutoHidden: true }
                ? manager.FindVisualChildren<LayoutAnchorControl>().FirstOrDefault(c => ReferenceEquals(c.Model, model))
                : model is LayoutAnchorable ? manager.FindVisualChildren<LayoutAnchorablePaneControl>().FirstOrDefault(c => ReferenceEquals(c.Model, model.Parent))
                : manager.FindVisualChildren<LayoutTabItemBase>().FirstOrDefault(c => ReferenceEquals(c.Model, model));
            if (target?.ContextFlyout is MenuFlyout menu) menu.ShowAt(target);
            else status.Text = "Use the floating caption menu, or restore the tool into the workspace.";
        }
        void Status() => status.Text = $"Document: CanClose={editor.CanClose}, CanFloat={editor.CanFloat}. Tool: CanHide={explorer.CanHide}, CanAutoHide={explorer.CanAutoHide}, CanDockAsDocument={explorer.CanDockAsTabbedDocument}. Cancel close={cancelled}; application Float override={overridden}, enabled={queryEnabled}.";
        void Add(string text, Action action) { var button = new Button { Content = text, FontSize = 12, Padding = new(8, 4, 8, 4) }; button.Click += (_, _) => action(); actions.Children.Add(button); }
    }
    private sealed class MenuLabCommand(Action execute, Func<bool> canExecute) : ICommand
    {
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => canExecute();
        public void Execute(object? parameter) { if (canExecute()) execute(); }
        public void Raise() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
