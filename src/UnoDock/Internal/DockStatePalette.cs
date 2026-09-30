namespace UnoDock.Internal;
/// <summary>State-specific chrome brushes and shape options. Each value falls
/// back to the base palette slot that historically painted the same surface, so
/// a theme or application that omits a key keeps the existing presentation.
/// Optional foregrounds stay null when unspecified; the caller then applies its
/// established primary/secondary foreground rule.</summary>
internal sealed record DockStatePalette(Brush Workspace, Brush Splitter, Brush DocumentTabStrip, Brush DocumentTab, Brush SelectedDocumentTab, Brush ActiveDocumentTab, Brush? DocumentTabForeground, Brush? SelectedDocumentTabForeground, Brush? ActiveDocumentTabForeground, Brush? TabHover, Brush ToolTabStrip, Brush ToolTab, Brush SelectedToolTab, Brush? ToolTabForeground, Brush? SelectedToolTabForeground, Brush ToolTitle, Brush ActiveToolTitle, Brush? ToolTitleForeground, Brush? ActiveToolTitleForeground, Brush PaneBorder, Brush ActiveDocumentPaneBorder, Brush SelectedTabIndicator, Brush ActiveTabIndicator, Brush Rail, Brush? AnchorTab, Brush? AnchorTabForeground, Brush FloatingBorder, Brush ActiveFloatingBorder, Brush TabBorder, DockTabShape DocumentTabShape = DockTabShape.Rectangle, DockTabIndicatorPlacement IndicatorPlacement = DockTabIndicatorPlacement.Top, bool BoldSelectedTab = false, double DocumentTabSpacing = 0, double PaneBorderThickness = 1, double FloatingBorderThickness = 0)
{
    /// <summary>Frame of the active document pane. A theme may give each side its
        /// own width, for example a band only above and below the content.</summary>
        internal Thickness ActiveDocumentPaneBorderThickness
    {
        get;
        init;
    } = new(1);
    /// <summary>Frame of a document pane that does not own the active content.</summary>
    internal Brush? DocumentPaneBorder
    {
        get;
        init;
    }
    internal Thickness DocumentPaneBorderThickness
    {
        get;
        init;
    } = new(1);
    /// <summary>True when the document frame wraps only the content below the
        /// tab strip instead of the whole pane, tabs included.</summary>
        internal bool DocumentFrameAroundContent
    {
        get;
        init;
    }
    /// <summary>Hairline drawn directly around pane content (null draws none).</summary>
    internal Brush? ContentBorder
    {
        get;
        init;
    }
    internal double DocumentFrameCornerRadius
    {
        get;
        init;
    }
    internal double ToolTitleCornerRadius
    {
        get;
        init;
    }
    /// <summary>Glyph brush of caption buttons on inactive/active tool titles;
        /// null uses the matching title foreground.</summary>
        internal Brush? CaptionButtonForeground
    {
        get;
        init;
    }
    internal Brush? ActiveCaptionButtonForeground
    {
        get;
        init;
    }
    /// <summary>Hover and pressed presentation of caption and title buttons; a
        /// null brush keeps the base palette's hover/pressed fill or the resting
        /// glyph brush.</summary>
        internal Brush? ChromeButtonHover
    {
        get;
        init;
    }
    internal Brush? ChromeButtonPressed
    {
        get;
        init;
    }
    internal Brush? ChromeButtonHoverForeground
    {
        get;
        init;
    }
    internal Brush? ChromeButtonPressedForeground
    {
        get;
        init;
    }
    internal Brush? ChromeButtonHoverBorder
    {
        get;
        init;
    }
    /// <summary>Unselected document tabs are this much lower than the selected tab.</summary>
    internal double SelectedTabRaise
    {
        get;
        init;
    }
    /// <summary>Space before the first document tab.</summary>
    internal double DocumentTabStripInset
    {
        get;
        init;
    }
    /// <summary>Whether a floating document window shows the window-position menu button.</summary>
    internal bool FloatingDocumentMenuButton
    {
        get;
        init;
    } = true;

    internal static DockStatePalette From(DockPalette p) => new(p.Header, p.Header, p.Header, p.Tab, p.Surface, p.Surface, null, null, null, null, p.Header, p.Tab, p.Surface, null, null, p.Header, p.ActiveTitle, null, null, p.Border, p.Border, p.Border, p.Accent, p.Header, null, null, p.Border, p.Border, p.Border);
    /// <summary>Resolve every optional key through <paramref name = "find"/>; a
        /// missing or wrongly typed value keeps the historical default.</summary>
        internal static DockStatePalette Resolve(DockPalette p, Func<string, object?> find)
    {
        var d = From(p);
        Brush B(string key, Brush fallback) => find(key) as Brush ?? fallback;
        Brush? O(string key) => find(key) as Brush;
        double N(string key, double fallback, double min, double max) => find(key) is double value && double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
        Thickness T(string key, Thickness fallback) => DockThicknessResource.Read(find(key), 0, 8) ?? fallback;
        var workspace = B("WorkspaceBrush", d.Workspace);
        var selectedDocument = B("SelectedDocumentTabBrush", d.SelectedDocumentTab);
        var paneBorder = B("PaneBorderBrush", d.PaneBorder);
        var paneThickness = N("PaneBorderThickness", d.PaneBorderThickness, 0, 8);
        var titleBrush = B("ToolTitleBrush", d.ToolTitle);
        var documentThickness = T("DocumentPaneBorderThickness", new(paneThickness));
        return new(workspace, B("SplitterBrush", workspace), B("DocumentTabStripBrush", d.DocumentTabStrip), B("DocumentTabBrush", d.DocumentTab), selectedDocument, B("ActiveDocumentTabBrush", selectedDocument), O("DocumentTabForegroundBrush"), O("SelectedDocumentTabForegroundBrush"), O("ActiveDocumentTabForegroundBrush"), O("TabHoverBrush"), B("ToolTabStripBrush", d.ToolTabStrip), B("ToolTabBrush", d.ToolTab), B("SelectedToolTabBrush", d.SelectedToolTab), O("ToolTabForegroundBrush"), O("SelectedToolTabForegroundBrush"), titleBrush, B("ActiveToolTitleBrush", d.ActiveToolTitle), O("ToolTitleForegroundBrush"), O("ActiveToolTitleForegroundBrush"), paneBorder, B("ActiveDocumentPaneBorderBrush", paneBorder), B("SelectedTabIndicatorBrush", d.SelectedTabIndicator), B("ActiveTabIndicatorBrush", d.ActiveTabIndicator), B("RailBrush", workspace), O("AnchorTabBrush"), O("AnchorTabForegroundBrush"), B("FloatingBorderBrush", d.FloatingBorder), B("ActiveFloatingBorderBrush", d.ActiveFloatingBorder), B("TabBorderBrush", d.TabBorder), Enum.TryParse<DockTabShape>(find("DocumentTabShape") as string, true, out var shape) ? shape : d.DocumentTabShape, Enum.TryParse<DockTabIndicatorPlacement>(find("TabIndicatorPlacement") as string, true, out var placement) ? placement : d.IndicatorPlacement, find("BoldSelectedTab") is bool bold ? bold : d.BoldSelectedTab, N("DocumentTabSpacing", d.DocumentTabSpacing, 0, 16), paneThickness, N("FloatingBorderThickness", d.FloatingBorderThickness, 0, 8))
        {
            DocumentPaneBorder = O("DocumentPaneBorderBrush"),
            DocumentPaneBorderThickness = documentThickness,
            ActiveDocumentPaneBorderThickness = T("ActiveDocumentPaneBorderThickness", documentThickness),
            DocumentFrameAroundContent = string.Equals(find("DocumentPaneBorderPlacement") as string, "Content", StringComparison.OrdinalIgnoreCase),
            ContentBorder = O("ContentBorderBrush"),
            DocumentFrameCornerRadius = N("DocumentPaneFrameCornerRadius", 0, 0, 12),
            ToolTitleCornerRadius = N("ToolTitleCornerRadius", 0, 0, 12),
            CaptionButtonForeground = O("CaptionButtonForegroundBrush"),
            ActiveCaptionButtonForeground = O("ActiveCaptionButtonForegroundBrush"),
            ChromeButtonHover = O("ChromeButtonHoverBrush"),
            ChromeButtonPressed = O("ChromeButtonPressedBrush"),
            ChromeButtonHoverForeground = O("ChromeButtonHoverForegroundBrush"),
            ChromeButtonPressedForeground = O("ChromeButtonPressedForegroundBrush"),
            ChromeButtonHoverBorder = O("ChromeButtonHoverBorderBrush"),
            SelectedTabRaise = N("SelectedTabRaise", 0, 0, 8),
            DocumentTabStripInset = N("DocumentTabStripInset", 0, 0, 64),
            FloatingDocumentMenuButton = find("FloatingDocumentMenuButton") is not false
        };
    }
}
