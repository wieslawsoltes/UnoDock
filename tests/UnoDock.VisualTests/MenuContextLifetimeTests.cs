using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.UI.Xaml.Data;
using Xceed.Wpf.AvalonDock.Controls;

namespace UnoDock.Testing;

/// <summary>Reentrant application callbacks must not corrupt temporary flyout contexts.</summary>
public static class MenuContextLifetimeTests
{
    public static Task<int> Run(string output)
    {
        var tests = new TestRunner(); Register(tests); return tests.Run(output, "menu-context-lifetime");
    }
    private static void Register(TestRunner tests)
    {
        tests.Test("menu context replacement inside assignment wins for the complete item tree", () =>
        {
            var (menu, first, second) = Menu(); var original = new object(); var replacement = new object();
            first.DataContextChanged += (_, _) => { if (ReferenceEquals(first.DataContext, original)) Apply(menu, replacement); };
            Apply(menu, original);
            Check.Same(replacement, first.DataContext); Check.Same(replacement, second.DataContext);
            Clear(menu); Unassigned(first); Unassigned(second);
        });
        tests.Test("menu clear callback can reopen the same context without losing its new assignment", () =>
        {
            var (menu, first, second) = Menu(); var context = new object(); var reentered = false;
            Apply(menu, context);
            first.DataContextChanged += (_, _) =>
            {
                if (reentered || first.DataContext != null) return;
                reentered = true; Apply(menu, context);
            };
            Clear(menu);
            Check.True(reentered); Check.Same(context, first.DataContext); Check.Same(context, second.DataContext);
            Clear(menu); Unassigned(first); Unassigned(second);
        });
        tests.Test("menu clear callback can replace the context without invalidating enumeration", () =>
        {
            var (menu, first, second) = Menu(); var old = new object(); var next = new object(); var changed = false;
            Apply(menu, old);
            first.DataContextChanged += (_, _) =>
            {
                if (changed || first.DataContext != null) return;
                changed = true; Apply(menu, next);
            };
            Clear(menu); Check.Same(next, first.DataContext); Check.Same(next, second.DataContext);
            Clear(menu); Unassigned(first); Unassigned(second);
        });
        tests.Test("menu clear during assignment prevents stale assignments to later rows", () =>
        {
            var (menu, first, second) = Menu(); var context = new object();
            first.DataContextChanged += (_, _) => { if (ReferenceEquals(first.DataContext, context)) Clear(menu); };
            Apply(menu, context); Unassigned(first); Unassigned(second);
        });
        tests.Test("menu context cleanup preserves explicit application edits made during assignment", () =>
        {
            var (menu, first, second) = Menu(); var context = new object(); var owned = new object();
            first.DataContextChanged += (_, _) => { if (ReferenceEquals(first.DataContext, context)) first.DataContext = owned; };
            Apply(menu, context); Clear(menu); Check.Same(owned, first.DataContext); Unassigned(second);
        });
        tests.Test("menu assignment preserves an existing local binding", () =>
        {
            var (menu, first, second) = Menu(); var value = new object();
            var binding = new Binding { Source = value };
            first.SetBinding(FrameworkElement.DataContextProperty, binding);
            Apply(menu, new object()); Clear(menu);
            Check.Same(value, first.DataContext);
            Check.Same(binding, first.GetBindingExpression(FrameworkElement.DataContextProperty).ParentBinding);
            Unassigned(second);
        });
        tests.Test("menu context null is a scoped local value, not an explicit application override", () =>
        {
            var (menu, first, second) = Menu(); Apply(menu, null);
            Check.Equal<object?>(null, first.ReadLocalValue(FrameworkElement.DataContextProperty));
            Clear(menu); Unassigned(first); Unassigned(second);
        });
        tests.Test("menu removed row has no leftover context after cleanup", () =>
        {
            var (menu, first, second) = Menu(); var context = new object();
            first.DataContextChanged += (_, _) => { if (ReferenceEquals(first.DataContext, context)) menu.Items.Remove(first); };
            Apply(menu, context); Clear(menu); Unassigned(first); Unassigned(second);
        });
        tests.Test("menu recursive submenu assignment redirects all descendants", () =>
        {
            var (menu, first, second) = Menu(); var sub = new MenuFlyoutSubItem { Text = "Nested" };
            var child = new MenuFlyoutItem { Text = "Child" }; sub.Items.Add(child); menu.Items.Add(sub);
            var original = new object(); var replacement = new object();
            child.DataContextChanged += (_, _) => { if (ReferenceEquals(child.DataContext, original)) Apply(menu, replacement); };
            Apply(menu, original);
            Check.Same(replacement, first.DataContext); Check.Same(replacement, second.DataContext);
            Check.Same(replacement, sub.DataContext); Check.Same(replacement, child.DataContext);
            Clear(menu); Unassigned(first); Unassigned(second); Unassigned(sub); Unassigned(child);
        });
        tests.Test("menu callback can retain one application context while clearing the temporary scope", () =>
        {
            var (menu, first, second) = Menu(); var context = new object(); var application = new object();
            Apply(menu, context); second.DataContext = application; Clear(menu);
            Unassigned(first); Check.Same(application, second.DataContext);
            Apply(menu, new object()); Check.Same(application, second.DataContext); Clear(menu);
        });
        tests.Test("menu context operation releases partial assignments after a callback exception", () =>
        {
            var (menu, first, second) = Menu(); var context = new object(); var error = new InvalidOperationException("application callback");
            second.DataContextChanged += (_, _) => { if (ReferenceEquals(second.DataContext, context)) throw error; };
            var actual = Check.Throws<InvalidOperationException>(() => Apply(menu, context)); Check.Same(error, actual);
            Unassigned(first); Unassigned(second);
            var next = new object(); Apply(menu, next); Check.Same(next, first.DataContext); Clear(menu);
        });
        tests.Test("ContextMenuEx context replacement during old-scope cleanup agrees with its final property", () =>
        {
            var menu = new ContextMenuEx(); var first = new MenuFlyoutItem(); var second = new MenuFlyoutItem();
            menu.Items.Add(first); menu.Items.Add(second);
            var initial = new object(); var requested = new object(); var redirected = new object();
            menu.MenuDataContext = initial; var changed = false;
            first.DataContextChanged += (_, _) =>
            {
                if (changed || first.DataContext != null) return;
                changed = true; menu.MenuDataContext = redirected;
            };
            menu.MenuDataContext = requested;
            Check.Same(redirected, menu.MenuDataContext);
            Check.Same(redirected, first.DataContext); Check.Same(redirected, second.DataContext);
            menu.MenuDataContext = null; Clear(menu); Unassigned(first); Unassigned(second);
        });
        tests.Test("nonconvergent menu context callbacks are bounded and do not poison the next operation", () =>
        {
            var (menu, first, second) = Menu(); var a = new object(); var b = new object(); var loop = true; var calls = 0;
            first.DataContextChanged += (_, _) =>
            {
                if (!loop || first.DataContext == null) return;
                calls++; Apply(menu, ReferenceEquals(first.DataContext, a) ? b : a);
            };
            var error = Check.Throws<InvalidOperationException>(() => Apply(menu, a));
            Check.True(error.Message.Contains("converge", StringComparison.Ordinal)); Check.True(calls > 1 && calls < 100);
            Unassigned(first); Unassigned(second); loop = false;
            Apply(menu, b); Check.Same(b, first.DataContext); Check.Same(b, second.DataContext); Clear(menu);
        });
        tests.Test("menu assignment and cleanup exceptions retain both causes while releasing other rows", () =>
        {
            var (menu, first, second) = Menu(); var context = new object(); var assigned = false;
            var original = new InvalidOperationException("assignment callback"); var cleanup = new InvalidOperationException("cleanup callback");
            first.DataContextChanged += (_, _) =>
            {
                if (ReferenceEquals(first.DataContext, context)) assigned = true;
                else if (assigned && first.DataContext == null) throw cleanup;
            };
            second.DataContextChanged += (_, _) => { if (ReferenceEquals(second.DataContext, context)) throw original; };
            var error = Check.Throws<AggregateException>(() => Apply(menu, context));
            Check.Equal(2, error.InnerExceptions.Count); Check.Same(original, error.InnerExceptions[0]); Check.Same(cleanup, error.InnerExceptions[1]);
            Unassigned(first); Unassigned(second); assigned = false; Clear(menu);
        });
    }
    private static (MenuFlyout menu, MenuFlyoutItem first, MenuFlyoutItem second) Menu()
    {
        var menu = new MenuFlyout(); var first = new MenuFlyoutItem { Text = "First" }; var second = new MenuFlyoutItem { Text = "Second" };
        menu.Items.Add(first); menu.Items.Add(second); return (menu, first, second);
    }
    private static void Unassigned(MenuFlyoutItemBase item) => Check.Same(DependencyProperty.UnsetValue, item.ReadLocalValue(FrameworkElement.DataContextProperty));
    private static void Apply(MenuFlyout menu, object? value) => Call("Apply", [menu, value]);
    private static void Clear(MenuFlyout menu) => Call("Clear", [menu]);
    private static void Call(string name, object?[] args)
    {
        var type = typeof(DockingManager).Assembly.GetType("Xceed.Wpf.AvalonDock.Internal.MenuContext", true)!;
        try { type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args); }
        catch (TargetInvocationException e) when (e.InnerException != null) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); }
    }
}
