using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Data;
using UnoDock.Controls;
using UnoDock.Layout.Serialization;

namespace UnoDock.Gallery;

[Bindable]
public sealed class WorkspaceTool(string contentId, string title, FrameworkElement view) : IDockContent
{
    public string ContentId { get; } = contentId;
    public string Title { get; } = title;
    public FrameworkElement View { get; } = view;
}
