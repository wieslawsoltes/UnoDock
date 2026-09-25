namespace UnoDock.Layout;

[Flags]
public enum AnchorableShowStrategy : byte
{
    Most = 1,
    Left = 2,
    Right = 4,
    Top = 16,
    Bottom = 32
}
