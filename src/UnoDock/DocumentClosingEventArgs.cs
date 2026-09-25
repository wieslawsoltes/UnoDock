using Microsoft.UI.Xaml.Input;
using UnoDock.Controls;
using UnoDock.Internal;
using UnoDock.Layout;
using UnoDock.Themes;

namespace UnoDock;

public class DocumentClosingEventArgs(LayoutDocument document) : CancelEventArgs
{
    public LayoutDocument Document { get; private set; } = document;
}
#if WINDOWS
#else
#endif
