using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Xml;

namespace UnoDock.Layout.Serialization;

public class LayoutSerializationCallbackEventArgs(LayoutContent model, object? previousContent) : CancelEventArgs
{
    public LayoutContent Model
    {
        get;
        private set;
    } = model;
    public object? Content
    {
        get;
        set;
    } = previousContent;
}
