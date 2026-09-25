using Microsoft.UI.Xaml.Input;
using UnoDock.Controls;
using UnoDock.Internal;
using UnoDock.Layout;
using UnoDock.Themes;

namespace UnoDock;
/// <summary>Portable event identity for the four AvalonDock extension events that WinUI cannot register natively.</summary>
public sealed record DockRoutedEvent(string Name);
#if WINDOWS
#else
#endif
