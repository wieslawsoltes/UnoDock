namespace UnoDock.Layout;

public class LayoutElementEventArgs(LayoutElement element) : EventArgs
{
    public LayoutElement Element { get; private set; } = element;
}
