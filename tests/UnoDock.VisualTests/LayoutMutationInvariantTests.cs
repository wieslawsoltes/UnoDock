using System.Collections.Specialized;
using System.ComponentModel;
using UnoDock.Layout;

namespace UnoDock.Testing;

internal static class LayoutMutationInvariantTests
{
    internal static Task<int> Run(string output)
    {
        var tests = new TestRunner();
        LayoutTransferRegressionTests.Add(tests);
        foreach (var operation in new[]
        {
            "insert",
            "remove",
            "replace",
            "clear",
            "move",
            "transfer",
            "slot"
        }

        )
        {
            foreach (var phase in new[]
            {
                "changing",
                "parent",
                "collection",
                "owner",
                "root"
            }

            )
            {
                if (operation == "move" && phase is "changing" or "parent")
                {
                    continue;
                }

                if (operation == "slot" && phase == "collection")
                {
                    continue;
                }

                tests.Test($"ownership: {operation}/{phase} exception preserves both directions", () =>
                {
                    var first = new LayoutDocument
                    {
                        Title = "First"
                    };
                    var second = new LayoutDocument
                    {
                        Title = "Second"
                    };
                    var incoming = new LayoutDocument
                    {
                        Title = "Incoming"
                    };
                    var source = new LayoutDocumentPane(first);
                    source.Children.Add(second);
                    var destination = new LayoutDocumentPane();
                    var floating = new LayoutDocumentFloatingWindow();
                    var panel = new LayoutPanel(source);
                    panel.Children.Add(destination);
                    var root = new LayoutRoot
                    {
                        RootPanel = panel
                    };
                    root.FloatingWindows.Add(floating);
                    if (operation == "slot")
                    {
                        floating.RootDocument = incoming;
                    }

                    var nodes = new LayoutElement[]
                    {
                        root,
                        panel,
                        source,
                        destination,
                        floating,
                        first,
                        second,
                        incoming
                    };
                    var failure = new InvalidOperationException("observer failure: " + phase);
                    var thrown = false;
                    void Fail()
                    {
                        AssertOwnership(nodes);
                        if (!thrown)
                        {
                            thrown = true;
                            throw failure;
                        }
                    }

                    PropertyChangingEventHandler changing = (_, args) =>
                    {
                        if (args.PropertyName == "Parent")
                            Fail();
                    };
                    PropertyChangedEventHandler parent = (_, args) =>
                    {
                        if (args.PropertyName == "Parent")
                            Fail();
                    };
                    NotifyCollectionChangedEventHandler collection = (_, _) => Fail();
                    PropertyChangedEventHandler owner = (_, args) =>
                    {
                        if (args.PropertyName is "ChildrenCount" or "RootDocument")
                            Fail();
                    };
                    EventHandler updated = (_, _) => Fail();
                    if (phase == "changing")
                        foreach (var node in nodes)
                            node.PropertyChanging += changing;
                    if (phase == "parent")
                        foreach (var node in nodes)
                            node.PropertyChanged += parent;
                    if (phase == "collection")
                    {
                        source.Children.CollectionChanged += collection;
                        destination.Children.CollectionChanged += collection;
                    }

                    if (phase == "owner")
                        foreach (var node in nodes)
                            node.PropertyChanged += owner;
                    if (phase == "root")
                        root.Updated += updated;
                    Exception? observed = null;
                    try
                    {
                        switch (operation)
                        {
                            case "insert":
                                source.Children.Add(incoming);
                                break;
                            case "remove":
                                source.Children.Remove(first);
                                break;
                            case "replace":
                                source.Children[0] = incoming;
                                break;
                            case "clear":
                                source.Children.Clear();
                                break;
                            case "move":
                                source.Children.Move(0, 1);
                                break;
                            case "transfer":
                                destination.Children.Add(first);
                                break;
                            default:
                                floating.RootDocument = first;
                                break;
                        }
                    }
                    catch (Exception error)
                    {
                        observed = error;
                    }
                    finally
                    {
                        foreach (var node in nodes)
                        {
                            node.PropertyChanging -= changing;
                            node.PropertyChanged -= parent;
                            node.PropertyChanged -= owner;
                        }

                        source.Children.CollectionChanged -= collection;
                        destination.Children.CollectionChanged -= collection;
                        root.Updated -= updated;
                    }

                    Check.True(thrown, "The intended observer did not execute.");
                    Check.True(Contains(observed, failure), "The original observer exception was lost.");
                    AssertOwnership(nodes);
                    // Failure must not poison notification depth or a later edit.
                    var followUp = new LayoutDocument
                    {
                        Title = "Follow-up"
                    };
                    destination.Children.Add(followUp);
                    Check.Same(destination, followUp.Parent);
                    destination.Children.Remove(followUp);
                    Check.True(followUp.Parent == null);
                    AssertOwnership(nodes);
                });
            }
        }

        tests.Test("ownership: every parent and collection callback sees a complete tree", () =>
        {
            var source = new LayoutDocumentPane();
            var target = new LayoutDocumentPane();
            var panel = new LayoutPanel(source);
            panel.Children.Add(target);
            var root = new LayoutRoot
            {
                RootPanel = panel
            };
            var child = new LayoutDocument();
            var nodes = new LayoutElement[]
            {
                source,
                target,
                panel,
                root,
                child
            };
            child.PropertyChanging += (_, _) => AssertOwnership(nodes);
            child.PropertyChanged += (_, _) => AssertOwnership(nodes);
            source.Children.CollectionChanged += (_, _) => AssertOwnership(nodes);
            target.Children.CollectionChanged += (_, _) => AssertOwnership(nodes);
            source.Children.Add(child);
            target.Children.Add(child);
            target.Children.Clear();
            AssertOwnership(nodes);
        });
        tests.Test("ownership: reentrant detach transfer is not reclaimed", () =>
        {
            var child = new LayoutDocument();
            var source = new LayoutDocumentPane(child);
            var target = new LayoutDocumentPane();
            var application = new LayoutDocumentPane();
            var moved = false;
            child.PropertyChanged += (_, args) =>
            {
                if (!moved && args.PropertyName == "Parent" && child.Parent == null)
                {
                    moved = true;
                    application.Children.Add(child);
                }
            };
            target.Children.Add(child);
            Check.Same(application, child.Parent);
            Check.Equal(0, source.Children.Count);
            Check.Equal(0, target.Children.Count);
            Check.Equal(1, application.Children.Count);
        });
        tests.Test("ownership: a pre-change callback replacing a slot wins", () =>
        {
            var original = new LayoutDocument();
            var requested = new LayoutDocument();
            var successor = new LayoutDocument();
            var window = new LayoutDocumentFloatingWindow
            {
                RootDocument = original
            };
            var redirected = false;
            original.PropertyChanging += (_, args) =>
            {
                if (!redirected && args.PropertyName == "Parent")
                {
                    redirected = true;
                    window.RootDocument = successor;
                }
            };
            window.RootDocument = requested;
            Check.Same(successor, window.RootDocument);
            Check.True(original.Parent == null && requested.Parent == null);
            AssertOwnership([window, original, requested, successor]);
        });
        tests.Test("ownership: throwing collection observer does not skip pane repair", () =>
        {
            var child = new LayoutDocument();
            var pane = new LayoutDocumentPane();
            var failure = new InvalidOperationException("collection");
            pane.Children.CollectionChanged += (_, _) => throw failure;
            var observed = Check.Throws<InvalidOperationException>(() => pane.Children.Add(child));
            Check.Same(failure, observed);
            Check.Same(child, pane.SelectedContent);
            Check.Same(pane, child.Parent);
        });
        tests.Test("ownership: independent callback failures retain chronological identity", () =>
        {
            var pane = new LayoutDocumentPane();
            var child = new LayoutDocument();
            var first = new InvalidOperationException("collection");
            var second = new InvalidOperationException("parent");
            pane.Children.CollectionChanged += (_, _) => throw first;
            child.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == "Parent")
                    throw second;
            };
            var observed = Check.Throws<AggregateException>(() => pane.Children.Add(child));
            Check.Same(first, observed.InnerExceptions[0]);
            Check.Same(second, observed.InnerExceptions[1]);
            AssertOwnership([pane, child]);
        });
        foreach (var trigger in new[]
        {
            "deactivate-changing",
            "deactivate-property",
            "deactivate-event",
            "activate-property",
            "select",
            "timestamp",
            "root"
        }

        )
        {
            foreach (var throws in new[]
            {
                false,
                true
            }

            )
            {
                tests.Test($"activation: {trigger}, throws={throws}, last request wins", () =>
                {
                    var a = new LayoutDocument
                    {
                        Title = "A"
                    };
                    var b = new LayoutDocument
                    {
                        Title = "B"
                    };
                    var c = new LayoutDocument
                    {
                        Title = "C"
                    };
                    var pane = new LayoutDocumentPane(a);
                    pane.Children.Add(b);
                    pane.Children.Add(c);
                    var root = new LayoutRoot
                    {
                        RootPanel = new LayoutPanel(pane)
                    };
                    root.ActiveContent = a;
                    var redirected = false;
                    var failure = new InvalidOperationException("activation observer");
                    void Redirect()
                    {
                        AssertActive(root, [a, b, c]);
                        if (redirected)
                            return;
                        redirected = true;
                        root.ActiveContent = c;
                        if (throws)
                            throw failure;
                    }

                    a.PropertyChanging += (_, args) =>
                    {
                        if (trigger == "deactivate-changing" && args.PropertyName == "IsActive")
                            Redirect();
                    };
                    a.PropertyChanged += (_, args) =>
                    {
                        if (trigger == "deactivate-property" && args.PropertyName == "IsActive")
                            Redirect();
                    };
                    a.IsActiveChanged += (_, _) =>
                    {
                        if (trigger == "deactivate-event" && !a.IsActive)
                            Redirect();
                    };
                    b.PropertyChanged += (_, args) =>
                    {
                        if (trigger == "activate-property" && args.PropertyName == "IsActive" || trigger == "select" && args.PropertyName == "IsSelected" || trigger == "timestamp" && args.PropertyName == "LastActivationTimeStamp")
                            Redirect();
                    };
                    root.PropertyChanged += (_, args) =>
                    {
                        if (trigger == "root" && args.PropertyName == "ActiveContent")
                            Redirect();
                    };
                    Exception? observed = null;
                    try
                    {
                        root.ActiveContent = b;
                    }
                    catch (Exception error)
                    {
                        observed = error;
                    }

                    Check.True(redirected);
                    if (throws)
                        Check.True(Contains(observed, failure));
                    else
                        Check.True(observed == null);
                    Check.Same(c, root.ActiveContent);
                    Check.Same(c, root.LastFocusedDocument);
                    AssertActive(root, [a, b, c]);
                });
            }
        }

        tests.Test("activation: a later request for the current item withdraws a queued successor", () =>
        {
            var a = new LayoutDocument();
            var b = new LayoutDocument();
            var c = new LayoutDocument();
            var pane = new LayoutDocumentPane(a);
            pane.Children.Add(b);
            pane.Children.Add(c);
            var root = new LayoutRoot
            {
                RootPanel = new LayoutPanel(pane)
            };
            root.ActiveContent = a;
            a.IsActiveChanged += (_, _) =>
            {
                if (!a.IsActive)
                {
                    root.ActiveContent = c;
                    root.ActiveContent = b;
                }
            };
            root.ActiveContent = b;
            Check.Same(b, root.ActiveContent);
            AssertActive(root, [a, b, c]);
        });
        tests.Test("activation: pre-change exception leaves the prior active state intact", () =>
        {
            var a = new LayoutDocument();
            var b = new LayoutDocument();
            var pane = new LayoutDocumentPane(a);
            pane.Children.Add(b);
            var root = new LayoutRoot
            {
                RootPanel = new LayoutPanel(pane)
            };
            root.ActiveContent = a;
            var failure = new InvalidOperationException("pre-change");
            b.PropertyChanging += (_, args) =>
            {
                if (args.PropertyName == "IsActive")
                    throw failure;
            };
            Check.Same(failure, Check.Throws<InvalidOperationException>(() => root.ActiveContent = b));
            Check.Same(a, root.ActiveContent);
            AssertActive(root, [a, b]);
        });
        return tests.Run(output, "layout-mutation-invariants");
    }

    private static bool Contains(Exception? observed, Exception expected) => ReferenceEquals(observed, expected) || observed is AggregateException aggregate && aggregate.InnerExceptions.Any(error => Contains(error, expected));
    private static void AssertOwnership(IEnumerable<LayoutElement> nodes)
    {
        var all = nodes.ToArray();
        foreach (var owner in all.OfType<ILayoutContainer>())
        {
            var children = owner.Children.ToArray();
            Check.Equal(children.Length, children.Distinct(ReferenceEqualityComparer.Instance).Count());
            foreach (var child in all)
            {
                var listed = children.Any(candidate => ReferenceEquals(candidate, child));
                Check.Equal(listed, ReferenceEquals(child.Parent, owner));
            }
        }
    }

    private static void AssertActive(LayoutRoot root, LayoutContent[] contents)
    {
        foreach (var content in contents)
        {
            Check.Equal(ReferenceEquals(root.ActiveContent, content), content.IsActive);
        }
    }
}
