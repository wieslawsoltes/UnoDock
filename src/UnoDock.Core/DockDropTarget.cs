namespace UnoDock.Core;

public readonly record struct DockDropTarget(string Id, DockRect Bounds, bool AcceptsDocuments, bool AcceptsAnchorables, int Priority = 0);
