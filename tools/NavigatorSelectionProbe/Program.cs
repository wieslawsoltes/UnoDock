// Independently authored public/protected API observations. No original bodies,
// templates, resources, IL or non-public members are inspected or exported.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml.Linq;
using Xceed.Wpf.AvalonDock;
using Xceed.Wpf.AvalonDock.Controls;
using Xceed.Wpf.AvalonDock.Layout;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 1)
            throw new ArgumentException("Usage: NavigatorSelectionProbe <output-directory>");
        Directory.CreateDirectory(args[0]);
        var app = new Application
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown
        };
        var failure = 0;
        app.Startup += (_, __) =>
        {
            var manager = new DockingManager();
            var owner = new Window
            {
                Content = manager,
                Width = 900,
                Height = 600,
                ShowInTaskbar = false
            };
            owner.Loaded += (_, ___) => owner.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                try
                {
                    var result = new XElement("NavigatorSelectionObservations", new XAttribute("schema", 1), new XAttribute("method", "public-and-protected-api-only"));
                    foreach (var tool in new[]
                    {
                        false,
                        true
                    }

                    )
                        foreach (var scenario in new[]
                        {
                            "default",
                            "setvalue",
                            "null",
                            "same",
                            "veto",
                            "noop",
                            "disabled",
                            "closing-veto",
                            "closing-reselect",
                            "query-reselect",
                            "execute-reselect",
                            "query-throws",
                            "execute-throws",
                            "closed-repeat"
                        }

                        )
                        {
                            result.Add(Observe(manager, owner, tool, scenario));
                            new XDocument(result).Save(Path.Combine(args[0], "navigator-selection-observations.xml"));
                        }

                    Console.WriteLine("Observed " + result.Elements("Case").Count() + " public navigator setter cases.");
                }
                catch (Exception error)
                {
                    failure = 1;
                    Console.Error.WriteLine(error);
                }
                finally
                {
                    owner.Close();
                    app.Shutdown();
                }
            }));
            owner.Show();
        };
        app.Run();
        return failure;
    }

    private static XElement Observe(DockingManager manager, Window owner, bool tool, string scenario)
    {
        var documents = new LayoutDocumentPane();
        var tools = new LayoutAnchorablePane();
        for (var i = 0; i < 3; i++)
        {
            documents.Children.Add(new LayoutDocument { ContentId = "d" + i, Title = "Document " + i, Content = "Owned document " + i });
            tools.Children.Add(new LayoutAnchorable { ContentId = "t" + i, Title = "Tool " + i, Content = "Owned tool " + i });
        }

        var panel = new LayoutPanel(tools);
        panel.Children.Add(documents);
        manager.Layout = new LayoutRoot
        {
            RootPanel = panel
        };
        documents.Children[0].IsActive = true;
        var ordinal = 0;
        foreach (var content in manager.Layout.Descendents().OfType<LayoutContent>())
            content.LastActivationTimeStamp = new DateTime(2000, 1, 1).AddSeconds(ordinal++);
        manager.UpdateLayout();
        var nav = new ObservedNavigator(manager)
        {
            Owner = owner
        };
        var row = new XElement("Case", new XAttribute("name", (tool ? "tool/" : "document/") + scenario));
        var trace = new List<string>();
        var recording = false;
        var closed = false;
        var once = false;
        var calls = 0;
        Action<string> log = text =>
        {
            if (recording)
                trace.Add(text);
        };
        nav.Trace = log;
        EventHandler active = (_, __) => log("active:" + Id(manager.Layout.ActiveContent));
        manager.ActiveContentChanged += active;
        CancelEventHandler closing = (_, e) =>
        {
            log("closing");
            if (scenario == "closing-veto")
            {
                e.Cancel = true;
                log("cancel-close");
            }

            if (scenario == "closing-reselect" && !once)
            {
                once = true;
                log("closing-reselect");
                nav.SelectedDocument = (LayoutDocumentItem)manager.GetLayoutItemFromModel(documents.Children[1]);
            }
        };
        nav.Closing += closing;
        nav.Closed += (_, __) =>
        {
            closed = true;
            log("closed");
        };
        try
        {
            nav.Show();
            nav.UpdateLayout();
            Pump(nav);
            var target = tool ? (LayoutItem)manager.GetLayoutItemFromModel(tools.Children[2]) : manager.GetLayoutItemFromModel(documents.Children[2]);
            var targetModel = tool ? (LayoutContent)tools.Children[2] : documents.Children[2];
            var original = target.ActivateCommand;
            if (scenario == "disabled")
                targetModel.IsEnabled = false;
            if (scenario == "veto" || scenario == "noop" || scenario.StartsWith("query-", StringComparison.Ordinal) || scenario.StartsWith("execute-", StringComparison.Ordinal))
            {
                target.ActivateCommand = new Command(() =>
                {
                    if (++calls > 10)
                        throw new InvalidOperationException("Probe command recursion bound.");
                    log("query");
                    if (scenario == "query-throws")
                        throw new ApplicationException("Observed query failure");
                    if (scenario == "query-reselect" && !once)
                    {
                        once = true;
                        log("query-reselect");
                        nav.SelectedDocument = (LayoutDocumentItem)manager.GetLayoutItemFromModel(documents.Children[1]);
                    }

                    return scenario != "veto";
                }, () =>
                {
                    log("execute");
                    if (scenario == "execute-throws")
                        throw new ApplicationException("Observed execution failure");
                    if (scenario == "execute-reselect" && !once)
                    {
                        once = true;
                        log("execute-reselect");
                        nav.SelectedDocument = (LayoutDocumentItem)manager.GetLayoutItemFromModel(documents.Children[1]);
                    }
                    else if (scenario != "noop")
                        original.Execute(null);
                });
            }

            Pump(nav);
            calls = 0;
            row.Add(Snapshot("before", manager, nav, closed));
            recording = true;
            Apply("first", () =>
            {
                if (scenario == "null")
                {
                    if (tool)
                        nav.SelectedAnchorable = null;
                    else
                        nav.SelectedDocument = null;
                }
                else if (scenario == "same")
                {
                    if (tool)
                        nav.SelectedAnchorable = nav.SelectedAnchorable;
                    else
                        nav.SelectedDocument = nav.SelectedDocument;
                }
                else if (scenario == "setvalue")
                    nav.SetValue(tool ? NavigatorWindow.SelectedAnchorableProperty : NavigatorWindow.SelectedDocumentProperty, target);
                else
                    Set(nav, target);
            }, row, trace, manager, nav, () => closed);
            if (scenario == "closed-repeat" || scenario == "closing-veto")
                Apply("same-again", () => Set(nav, target), row, trace, manager, nav, () => closed);
            recording = false;
            Pump(nav);
            row.Add(Snapshot("settled", manager, nav, closed));
            return row;
        }
        finally
        {
            recording = false;
            nav.Trace = null;
            nav.Closing -= closing;
            manager.ActiveContentChanged -= active;
            nav.Close();
        }
    }

    private static void Apply(string name, Action action, XElement row, List<string> trace, DockingManager manager, NavigatorWindow nav, Func<bool> closed)
    {
        trace.Clear();
        string failure = null;
        try
        {
            action();
        }
        catch (Exception error)
        {
            failure = error.GetType().FullName;
        }

        var step = new XElement("Step", new XAttribute("name", name), new XAttribute("exception", failure ?? "none"));
        foreach (var entry in trace)
            step.Add(new XElement("Event", entry));
        step.Add(Snapshot("immediate", manager, nav, closed()));
        row.Add(step);
    }

    private static XElement Snapshot(string phase, DockingManager manager, NavigatorWindow nav, bool closed) => new XElement("State", new XAttribute("phase", phase), new XAttribute("visible", nav.IsVisible), new XAttribute("closed", closed), new XAttribute("active", Id(manager.Layout.ActiveContent)), new XAttribute("document", Id(nav.SelectedDocument?.LayoutElement)), new XAttribute("tool", Id(nav.SelectedAnchorable?.LayoutElement)));
    private static string Id(LayoutContent content) => content == null ? "null" : content.ContentId ?? "unset-id";
    private static void Set(NavigatorWindow nav, LayoutItem item)
    {
        if (item is LayoutAnchorableItem tool)
            nav.SelectedAnchorable = tool;
        else
            nav.SelectedDocument = (LayoutDocumentItem)item;
    }

    private static void Pump(NavigatorWindow nav) => nav.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() =>
    {
    }));
    private sealed class Command : ICommand
    {
        private readonly Func<bool> _query;
        private readonly Action _execute;
        internal Command(Func<bool> query, Action execute)
        {
            _query = query;
            _execute = execute;
        }

        public bool CanExecute(object parameter) => _query();
        public void Execute(object parameter) => _execute();
        public event EventHandler CanExecuteChanged
        {
            add
            {
            }

            remove
            {
            }
        }
    }

    private sealed class ObservedNavigator : NavigatorWindow
    {
        internal Action<string> Trace;
        internal ObservedNavigator(DockingManager manager) : base(manager)
        {
        }

        protected override void OnSelectedDocumentChanged(DependencyPropertyChangedEventArgs e)
        {
            Trace?.Invoke("document-enter:" + Id((e.NewValue as LayoutDocumentItem)?.LayoutElement));
            try
            {
                base.OnSelectedDocumentChanged(e);
            }
            finally
            {
                Trace?.Invoke("document-exit");
            }
        }

        protected override void OnSelectedAnchorableChanged(DependencyPropertyChangedEventArgs e)
        {
            Trace?.Invoke("tool-enter:" + Id((e.NewValue as LayoutAnchorableItem)?.LayoutElement));
            try
            {
                base.OnSelectedAnchorableChanged(e);
            }
            finally
            {
                Trace?.Invoke("tool-exit");
            }
        }
    }
}
