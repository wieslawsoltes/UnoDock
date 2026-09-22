namespace UnoDock.VisualValidation;

// Application-owned content shared by the two black-box visual hosts.
// No theme, template, geometry, image or code from the reference is embedded here.
public static class SceneContent
{
    public const int Width = 1100, Height = 720;
    public const string Code = "using Xceed.Wpf.AvalonDock;\nusing Xceed.Wpf.AvalonDock.Layout;\n\nvar manager = new DockingManager();\nvar document = new LayoutDocument\n{\n    Title = \"Workspace.cs\",\n    ContentId = \"editor\"\n};\n\n// Dock, float, pin and restore the same content.\nmanager.Layout.RootPanel.Children.Add(\n    new LayoutDocumentPane(document));";
    public const string Readme = "UnoDock workspace\n\nDocuments keep their content when moved between panes.\nDrag a tab to split or combine groups.\n\nCtrl+Tab  Switch content\nCtrl+F4   Close document\nEscape    Cancel docking";
    public const string Notes = "Notes\n\nA second editor with independently retained state.";
    public const string Output = "Build started...\n1> UnoDock.Core -> net10.0\n2> UnoDock -> net10.0\n3> UnoDock.Gallery -> net10.0-desktop\n========== Build: 3 succeeded, 0 failed ==========";
    public static readonly string[] Explorer = { "SOLUTION 'WORKSPACE'", "▾  UnoDock.Gallery", "    ▾  Layout", "        Workspace.cs", "        Documents.cs", "        Tools.cs", "    ▸  Resources", "    ▸  Properties", "    App.xaml", "    Readme.md" };
    public static readonly string[] Toolbox = { "CONTROLS", "Pointer", "TextBlock", "TextBox", "Button", "TreeView", "DataGrid" };
    public static readonly string[] Properties = { "Workspace.cs", "", "Name             document1", "Title                Workspace.cs", "ContentId        editor", "CanClose         True", "CanFloat          True", "CanMove          True", "IsSelected       True", "IsActive           True" };
    public static readonly string[] Errors = { "0 Errors       0 Warnings       0 Messages" };
}
