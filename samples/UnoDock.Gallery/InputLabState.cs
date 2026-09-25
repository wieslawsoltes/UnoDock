using System.ComponentModel;
using System.Collections.Specialized;
using Microsoft.UI.Xaml.Data;
using UnoDock.Compatibility;
using UnoDock.Controls;

namespace UnoDock.Gallery;

[Microsoft.UI.Xaml.Data.Bindable]
public sealed class InputLabState : INotifyPropertyChanged
{
    private int _index;
    public bool VetoPress
    {
        get;
        set;
    }
    public bool VetoDrop
    {
        get;
        set;
    }
    public bool Redirect
    {
        get;
        set;
    }

    public int Index
    {
        get => _index;
        set
        {
            if (_index == value)
                return;
            _index = value;
            PropertyChanged?.Invoke(this, new(nameof(Index)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
