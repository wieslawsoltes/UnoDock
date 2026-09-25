using Microsoft.UI.Xaml.Input;
using UnoDock.Controls;
using UnoDock.Internal;
using UnoDock.Layout;
using UnoDock.Themes;

namespace UnoDock;
public sealed class DockEventArgs(LayoutContent content) : RoutedEventArgs
{ public LayoutContent Content { get; } = content; public bool Cancel { get; set; } }

    #if WINDOWS
    #else
    #endif
