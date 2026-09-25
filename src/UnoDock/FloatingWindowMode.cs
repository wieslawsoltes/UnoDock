using Microsoft.UI.Xaml.Input;
using UnoDock.Controls;
using UnoDock.Internal;
using UnoDock.Layout;
using UnoDock.Themes;

namespace UnoDock;
public enum FloatingWindowMode
{
    Auto,
    Native,
    InSurface
}
#if WINDOWS
#else
#endif
