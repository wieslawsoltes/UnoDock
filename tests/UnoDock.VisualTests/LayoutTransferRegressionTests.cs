using UnoDock.Layout;

namespace UnoDock.Testing;
/// <summary>Additional public-API regressions for the detach-to-attach boundary.</summary>
internal static class LayoutTransferRegressionTests
{
    internal static void Add(TestRunner tests)
    {
        foreach (var operation in new[]
        {
            "insert",
            "replace",
            "slot"
        }

        )
        {
            tests.Test("ownership: detach ABA revokes incoming " + operation, () =>
            {
                var child = new LayoutDocument
                {
                    Title = "Incoming"
                };
                var retained = new LayoutDocument
                {
                    Title = "Retained"
                };
                var source = new LayoutDocumentPane(child);
                var intermediary = new LayoutDocumentPane();
                var destination = new LayoutDocumentPane();
                var floating = new LayoutDocumentFloatingWindow();
                if (operation == "replace")
                {
                    destination.Children.Add(retained);
                }
                else if (operation == "slot")
                {
                    floating.RootDocument = retained;
                }

                var callbacks = 0;
                child.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName != "Parent" || child.Parent != null || callbacks != 0)
                    {
                        return;
                    }

                    callbacks++;
                    // Complete another real ownership operation, then return to
                    // the same null parent seen by the interrupted transfer.
                    intermediary.Children.Add(child);
                    intermediary.Children.Remove(child);
                };
                switch (operation)
                {
                    case "insert":
                        destination.Children.Add(child);
                        break;
                    case "replace":
                        destination.Children[0] = child;
                        break;
                    default:
                        floating.RootDocument = child;
                        break;
                }

                Check.Equal(1, callbacks);
                Check.True(child.Parent == null);
                Check.Equal(0, source.Children.Count);
                Check.Equal(0, intermediary.Children.Count);
                if (operation == "insert")
                {
                    Check.Equal(0, destination.Children.Count);
                }
                else if (operation == "replace")
                {
                    Check.Same(retained, destination.Children.Single());
                    Check.Same(destination, retained.Parent);
                }
                else
                {
                    Check.Same(retained, floating.RootDocument);
                    Check.Same(floating, retained.Parent);
                }

                // A new explicit transfer is allowed after the old one retires.
                destination.Children.Add(child);
                Check.Same(destination, child.Parent);
            });
        }

        foreach (var side in new[]
        {
            AnchorSide.Left,
            AnchorSide.Top,
            AnchorSide.Right,
            AnchorSide.Bottom
        }

        )
        {
            foreach (var phase in new[]
            {
                "changing",
                "changed"
            }

            )
            {
                tests.Test($"ownership: superseded {side} side setter preserves foreign direction at {phase}", () =>
                {
                    var source = new LayoutRoot();
                    var destination = new LayoutRoot();
                    var incoming = new LayoutAnchorSide();
                    var targetSide = side == AnchorSide.Top ? AnchorSide.Bottom : AnchorSide.Top;
                    var moved = false;
                    void Redirect(string? property)
                    {
                        if (property != "Parent" || moved)
                        {
                            return;
                        }

                        moved = true;
                        Assign(destination, targetSide, incoming);
                    }

                    incoming.PropertyChanging += (_, args) =>
                    {
                        if (phase == "changing")
                        {
                            Redirect(args.PropertyName);
                        }
                    };
                    incoming.PropertyChanged += (_, args) =>
                    {
                        if (phase == "changed")
                        {
                            Redirect(args.PropertyName);
                        }
                    };
                    Assign(source, side, incoming);
                    Check.True(moved);
                    Check.Same(destination, incoming.Parent);
                    Check.Equal(targetSide, incoming.Side);
                    Check.False(source.Children.Any(child => ReferenceEquals(child, incoming)));
                    Check.Equal(1, destination.Children.Count(child => ReferenceEquals(child, incoming)));
                });
            }
        }
    }

    private static void Assign(LayoutRoot root, AnchorSide side, LayoutAnchorSide value)
    {
        switch (side)
        {
            case AnchorSide.Left:
                root.LeftSide = value;
                break;
            case AnchorSide.Top:
                root.TopSide = value;
                break;
            case AnchorSide.Right:
                root.RightSide = value;
                break;
            case AnchorSide.Bottom:
                root.BottomSide = value;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(side));
        }
    }
}
