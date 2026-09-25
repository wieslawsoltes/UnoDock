using AutomationProperties = Microsoft.UI.Xaml.Automation.AutomationProperties;
using UnoDock.Controls;
using UnoDock.Themes;
using Windows.Storage;

namespace UnoDock.Gallery;
[Microsoft.UI.Xaml.Data.Bindable]
public sealed class Note(string contentId, string title, string text) : INotifyPropertyChanged, IDockContent
{
    private string _title = title, _text = text;
    public string ContentId { get; } = contentId;

    public string Title
    {
        get => _title;
        set
        {
            if (_title == value)
                return;
            _title = value;
            PropertyChanged?.Invoke(this, new(nameof(Title)));
        }
    }

    public string Text
    {
        get => _text;
        set
        {
            if (_text == value)
                return;
            _text = value;
            PropertyChanged?.Invoke(this, new(nameof(Text)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
