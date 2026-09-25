using Microsoft.UI.Xaml.Input;
using UnoDock.Controls;
using UnoDock.Internal;
using UnoDock.Layout;
using UnoDock.Themes;

namespace UnoDock;
#if WINDOWS
#else
#endif
/// <summary>Optional AOT-safe source-item description. Existing view models can instead use LayoutItemContainerStyle bindings.</summary>
public interface IDockContent
{
    string ContentId
    {
        get;
    }

    string Title
    {
        get;
    }
}
