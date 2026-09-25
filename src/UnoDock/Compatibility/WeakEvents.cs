namespace UnoDock.Compatibility;
/// <summary>Source-migration listener contract; collection transport uses INotifyCollectionChanged as managerType.</summary>
public interface IWeakEventListener
{
    bool ReceiveWeakEvent(Type managerType, object sender, EventArgs e);
}
