using Microsoft.UI.Xaml.Input;
using UnoDock.Controls;
using UnoDock.Internal;
using UnoDock.Layout;
using UnoDock.Themes;

namespace UnoDock;
public class LayoutFloatingWindowControlCollectionChangedEventArgs(NotifyCollectionChangedEventArgs collectionChangedEventArgs) : EventArgs
{ public NotifyCollectionChangedEventArgs CollectionChangedEventArgs { get; private set; } = collectionChangedEventArgs; }

    #if WINDOWS
    #else
    #endif
