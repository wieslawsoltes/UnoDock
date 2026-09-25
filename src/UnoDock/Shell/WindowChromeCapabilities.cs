using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Input;
using UnoDock;
using UnoDock.Controls;

namespace Microsoft.Windows.Shell;

[Flags]
public enum WindowChromeCapabilities { None = 0, ManagedFrame = 1, NativeCaption = 2, NativeResize = 4, Glass = 8, SystemMenu = 16 }
