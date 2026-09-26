using System.Windows.Input;
using UnoDock.Themes;

namespace UnoDock.Gallery;
/// <summary>Sample commands only. Neither this type nor page code constructs a layout or editor.</summary>
public sealed class XamlWorkspaceActions : INotifyPropertyChanged, IDisposable
{
    private readonly DockingManager _manager;
    private readonly FrameworkElement _scope;
    private readonly List<WorkspaceCommand> _commands = [];
    private readonly Dictionary<string, object?> _savedContent = new(StringComparer.Ordinal);
    private string? _snapshot;
    private bool _disposed;
    private string _status = "Ready. Edit, dock, float, auto-hide, or save a layout snapshot.";
    public XamlWorkspaceActions(DockingManager manager, FrameworkElement scope)
    {
        _manager = manager;
        _scope = scope;
        ToggleThemeCommand = Command(() =>
        {
            _scope.RequestedTheme = _scope.ActualTheme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;
            Status = "Theme: " + _scope.RequestedTheme;
        });
        CycleDensityCommand = Command(() =>
        {
            if (_manager.Theme is FluentTheme theme)
            {
                _manager.ChromeDensity = _manager.ChromeDensity switch
                {
                    DockChromeDensity.Compact => DockChromeDensity.Comfortable,
                    DockChromeDensity.Comfortable => DockChromeDensity.Spacious,
                    _ => DockChromeDensity.Compact
                };
                Status = "Density: " + _manager.ChromeDensity;
            }
            else
                Status = "This custom palette supplies its own XAML size tokens. Switch to Fluent to cycle density.";
        });
        FloatCommand = Command(() =>
        {
            if (_manager.Layout.ActiveContent is not { } item)
                return;
            if (item.IsFloating)
                item.Dock();
            else
                item.Float();
            Status = item.Title + (item.IsFloating ? " is floating." : " is docked.");
        });
        AutoHideCommand = Command(() =>
        {
            var tool = _manager.Layout.ActiveContent as LayoutAnchorable ?? _manager.Layout.Descendents().OfType<LayoutAnchorable>().FirstOrDefault();
            tool?.ToggleAutoHide();
            Status = "Tool auto-hide toggled.";
        });
        DirectionCommand = Command(() =>
        {
            _manager.FlowDirection = _manager.FlowDirection == FlowDirection.LeftToRight ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            Status = "Flow direction: " + _manager.FlowDirection;
        });
        SaveCommand = Command(Save);
        RestoreCommand = Command(Restore);
    }

    public ICommand ToggleThemeCommand
    {
        get;
    }
    public ICommand CycleDensityCommand
    {
        get;
    }
    public ICommand FloatCommand
    {
        get;
    }
    public ICommand AutoHideCommand
    {
        get;
    }
    public ICommand DirectionCommand
    {
        get;
    }
    public ICommand SaveCommand
    {
        get;
    }
    public ICommand RestoreCommand
    {
        get;
    }

    public string Status
    {
        get => _status;
        private set
        {
            _status = value;
            PropertyChanged?.Invoke(this, new(nameof(Status)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private ICommand Command(Action action)
    {
        var command = new WorkspaceCommand(() =>
        {
            try
            {
                action();
            }
            catch (Exception error)
            {
                Status = "Operation failed: " + error.Message;
            }
        }, () => !_disposed);
        _commands.Add(command);
        return command;
    }

    public void Save()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var writer = new StringWriter();
        new XmlLayoutSerializer(_manager).Serialize(writer);
        var contents = _manager.Layout.Descendents().OfType<LayoutContent>().Where(item => item.ContentId != null).ToDictionary(item => item.ContentId!, item => item.Content, StringComparer.Ordinal);
        _snapshot = writer.ToString();
        _savedContent.Clear();
        foreach (var pair in contents)
            _savedContent.Add(pair.Key, pair.Value);
        Status = "Saved an in-memory XML layout; editor content is retained by ContentId.";
    }

    public void Restore()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_snapshot == null)
        {
            Status = "Save a layout first.";
            return;
        }

        var serializer = new XmlLayoutSerializer(_manager);
        serializer.LayoutSerializationCallback += (_, args) =>
        {
            if (args.Model.ContentId is { } id && _savedContent.TryGetValue(id, out var content))
                args.Content = content;
            else if (args.Content == null)
                args.Cancel = true;
        };
        serializer.Deserialize(new StringReader(_snapshot));
        Status = "Restored the saved docking layout and retained application content.";
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        foreach (var command in _commands)
            command.Detach();
        _commands.Clear();
        _savedContent.Clear();
        _snapshot = null;
        PropertyChanged = null;
        _manager.Dispose();
    }
}
