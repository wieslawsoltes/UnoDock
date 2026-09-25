using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using UnoDock.Internal;
using Windows.System;

namespace UnoDock.Controls;
internal readonly record struct DockResizeRange(double Minimum, double Maximum, double Value, bool IsReadOnly)
{
    internal static DockResizeRange Unavailable => new(0, 0, 0, true);
}
