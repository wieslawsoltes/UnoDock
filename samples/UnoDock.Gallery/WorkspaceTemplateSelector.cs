using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Data;
using UnoDock.Controls;
using UnoDock.Layout.Serialization;

namespace UnoDock.Gallery;

internal sealed class WorkspaceTemplateSelector : DataTemplateSelector
{
    protected override DataTemplate SelectTemplateCore(object item) => item switch
    {
        WorkspaceDocument => (DataTemplate)Application.Current.Resources["WorkspaceDocumentTemplate"],
        WorkspaceTool => (DataTemplate)Application.Current.Resources["WorkspaceToolTemplate"],
        _ => null!
    };
    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container) => SelectTemplateCore(item);
}
