namespace UnoDock;
/// <summary>How document pane tabs are presented.</summary>
public enum DocumentTabStripMode
{
    /// <summary>The theme-painted docking tab strip (all themes).</summary>
    Docking,
    /// <summary>The platform's WinUI <c>TabView</c> strip with native Fluent
        /// visuals, close buttons and scrolling; docking gestures are unchanged.</summary>
        TabView
}
