namespace UnoDock.Core;
public readonly record struct DockMeasure(double Value, DockLengthUnit Unit, double Minimum = 0, double Desired = 0);
