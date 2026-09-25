using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows.Input;
using UnoDock.Controls;

namespace UnoDock.Testing;
/// <summary>Real-host regressions for the boundary between an authorized close and
/// application callbacks. These tests do not inspect the original implementation.</summary>
internal static class NavigatorRevocationTests
{
    internal static async Task<int> Run(string output)
    {
        var tests = new TestRunner();
        var window = new Window
        {
            Title = "UnoDock navigator revocation acceptance"
        };
        window.AppWindow.Resize(new() { Width = 1100, Height = 800 });
        var mutations = new (string Name, bool Revokes, Action<Fixture> Apply)[]
        {
            ("preview changed", true, f => f.Nav.PreviewDocument(f.Document(f.A))),
            ("preview changed and restored", true, f =>
            {
                f.Nav.PreviewDocument(f.Document(f.A));
                f.PreviewTarget();
            }),
            ("preview cleared", true, f => f.Nav.PreviewDocument(null)),
            ("property changed", true, f => f.AssignOther()),
            ("property changed and restored", true, f =>
            {
                f.AssignOther();
                f.AssignTarget();
            }),
            ("property cleared", true, f => f.ClearTarget()),
            ("command changed and restored", true, f =>
            {
                var command = f.Item.ActivateCommand;
                f.Item.ActivateCommand = null;
                f.Item.ActivateCommand = command;
            }),
            ("model disabled and restored", true, f =>
            {
                f.Target.IsEnabled = false;
                f.Target.IsEnabled = true;
            }),
            ("manager disabled and restored", true, f =>
            {
                f.Host.IsEnabled = false;
                f.Host.IsEnabled = true;
            }),
            ("navigator disabled and restored", true, f =>
            {
                f.Nav.IsEnabled = false;
                f.Nav.IsEnabled = true;
            }),
            ("parent removed and restored", true, f => f.Reinsert()),
            ("root replaced and restored", true, f =>
            {
                var root = f.Host.Layout;
                f.Host.Layout = new();
                f.Host.Layout = root;
            }),
            ("identical preview", false, f => f.PreviewTarget()),
            ("identical property", false, f => f.AssignTarget()),
            ("harmless title change", false, f => f.Target.Title = "Renamed without changing authority")
        };
        foreach (var tool in new[]
        {
            false,
            true
        }

        )
            foreach (var duringUnload in new[]
            {
                false,
                true
            }

            )
                foreach (var mutation in mutations)
                {
                    var label = (tool ? "tool" : "document") + "/" + (duringUnload ? "Unloaded" : "CanExecute") + "/" + mutation.Name;
                    tests.Test("close revocation: " + label, async () =>
                    {
                        using var f = new Fixture(window, tool);
                        await f.Show();
                        f.PreviewTarget();
                        var mutationsObserved = 0;
                        var queries = 0;
                        var executions = 0;
                        void Mutate()
                        {
                            mutationsObserved++;
                            mutation.Apply(f);
                        }

                        f.Item.ActivateCommand = new Command(() =>
                        {
                            queries++;
                            if (!duringUnload)
                                Mutate();
                            return true;
                        }, () =>
                        {
                            executions++;
                            f.Target.IsActive = true;
                        });
                        if (duringUnload)
                            f.Nav.Unloaded += (_, _) => Mutate();
                        Surface(f.Host, "CloseNavigator", true);
                        Check.Equal(1, mutationsObserved);
                        Check.Equal(duringUnload && mutation.Revokes ? 0 : 1, queries);
                        Check.Equal(mutation.Revokes ? 0 : 1, executions);
                        Check.False(f.Visible);
                        Check.Same(mutation.Revokes ? f.A : f.Target, f.Host.Layout.ActiveContent);
                        // A retained detached view cannot create a second activation.
                        Call(f.Nav, "CommitSelection");
                        Check.Equal(mutation.Revokes ? 0 : 1, executions);
                    });
                }

        foreach (var closed in new[]
        {
            false,
            true
        }

        )
            foreach (var mutation in mutations.Take(10).Where(m => !m.Name.StartsWith("property", StringComparison.Ordinal)))
                tests.Test("direct tool revocation: " + (closed ? "Closed/" : "Closing/") + mutation.Name, async () =>
                {
                    using var f = new Fixture(window, true);
                    await f.Show();
                    f.Nav.PreviewDocument(f.Document(f.A));
                    var executions = 0;
                    var observed = 0;
                    // Reentrant direct property assignments may be serialized as a new
                    // valid request, so this lifecycle matrix uses preview/authority
                    // mutations only; DP request ordering has its own exact replay suite.
                    void Mutate()
                    {
                        observed++;
                        mutation.Apply(f);
                    }

                    f.Item.ActivateCommand = new Command(() => true, () => executions++);
                    if (closed)
                        f.Nav.Closed += (_, _) => Mutate();
                    else
                        f.Nav.Closing += (_, _) => Mutate();
                    f.AssignTarget();
                    Check.True(observed >= 1);
                    Check.Equal(0, executions);
                    Check.Same(f.A, f.Host.Layout.ActiveContent);
                });
        try
        {
            return await tests.Run(output, "navigator-revocation");
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly DockingManager Host = new()
        {
            Width = 1000,
            Height = 640,
            FloatingWindowMode = FloatingWindowMode.InSurface
        };
        internal readonly LayoutDocument A = new()
        {
            Title = "First",
            ContentId = "A"
        };
        private readonly LayoutDocument _b = new()
        {
            Title = "Second",
            ContentId = "B"
        };
        private readonly LayoutAnchorable _tool = new()
        {
            Title = "Properties",
            ContentId = "tool"
        };
        private readonly LayoutAnchorable _other = new()
        {
            Title = "Output",
            ContentId = "other"
        };
        private readonly LayoutDocumentPane _documents;
        private readonly LayoutAnchorablePane _tools;
        private readonly Window _window;
        internal readonly LayoutContent Target;
        internal readonly NavigatorWindow Nav;
        internal LayoutItem Item => Host.GetLayoutItemFromModel(Target);
        internal bool Visible => (bool)Surface(Host, "OwnsNavigator", Nav)!;

        internal Fixture(Window window, bool tool)
        {
            Target = tool ? _tool : _b;
            _documents = new(A);
            _documents.Children.Add(_b);
            _tools = new(_tool)
            {
                DockWidth = new(200)
            };
            _tools.Children.Add(_other);
            var panel = new LayoutPanel(_tools);
            panel.Children.Add(_documents);
            Host.Layout = new()
            {
                RootPanel = panel
            };
            A.IsActive = true;
            Nav = new(Host);
            _window = window;
            window.Content = Host;
            window.Activate();
        }

        internal async Task Show()
        {
            await Wait(() => Host.IsLoaded && Host.ActualWidth > 0);
            Surface(Host, "ShowNavigator", Nav);
            await Wait(() => Nav.IsLoaded && Nav.ActualHeight > 0);
        }

        internal LayoutDocumentItem Document(LayoutDocument doc) => (LayoutDocumentItem)Host.GetLayoutItemFromModel(doc);
        internal void PreviewTarget()
        {
            if (Item is LayoutDocumentItem document)
                Nav.PreviewDocument(document);
            else
                Nav.PreviewAnchorable((LayoutAnchorableItem)Item);
        }

        internal void AssignTarget()
        {
            if (Item is LayoutDocumentItem document)
                Nav.SelectedDocument = document;
            else
                Nav.SelectedAnchorable = (LayoutAnchorableItem)Item;
        }

        internal void AssignOther()
        {
            if (Target is LayoutDocument)
                Nav.SelectedDocument = Document(A);
            else
                Nav.SelectedAnchorable = (LayoutAnchorableItem)Host.GetLayoutItemFromModel(_other);
        }

        internal void ClearTarget()
        {
            if (Target is LayoutDocument)
                Nav.SelectedDocument = null;
            else
                Nav.SelectedAnchorable = null;
        }

        internal void Reinsert()
        {
            if (Target is LayoutDocument document)
            {
                _documents.Children.Remove(document);
                _documents.Children.Add(document);
            }
            else
            {
                _tools.Children.Remove(_tool);
                _tools.Children.Add(_tool);
            }
        }

        public void Dispose()
        {
            try
            {
                Host.Dispose();
            }
            finally
            {
                if (ReferenceEquals(_window.Content, Host))
                    _window.Content = null;
            }
        }
    }

    private sealed class Command(Func<bool> query, Action execute) : ICommand
    {
        public bool CanExecute(object? parameter) => query();
        public void Execute(object? parameter) => execute();
        public event EventHandler? CanExecuteChanged
        {
            add
            {
            }

            remove
            {
            }
        }
    }

    private static async Task Wait(Func<bool> predicate)
    {
        for (var i = 0; i < 100 && !predicate(); i++)
            await Task.Delay(20);
        Check.True(predicate(), "Navigator revocation fixture did not become ready.");
    }

    private static object? Call(object target, string name, params object? [] args)
    {
        try
        {
            return target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, args);
        }
        catch (TargetInvocationException error)when (error.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }
    }

    private static object? Surface(DockingManager host, string name, params object? [] args) => Call(typeof(DockingManager).GetProperty("Surface", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)!, name, args);
}
