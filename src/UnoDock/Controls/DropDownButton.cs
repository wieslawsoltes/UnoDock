using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using UnoDock.Compatibility;
using UnoDock.Internal;

namespace UnoDock.Controls;

public class DropDownButton : ToggleButton
{
    public static readonly DependencyProperty DropDownContextMenuProperty = DependencyProperty.Register(nameof(DropDownContextMenu), typeof(MenuFlyout), typeof(DropDownButton), new PropertyMetadata(null, (d, e) => ((DropDownButton)d).MenuChanged(e)));
    public static readonly DependencyProperty DropDownContextMenuDataContextProperty = DependencyProperty.Register(nameof(DropDownContextMenuDataContext), typeof(object), typeof(DropDownButton), new PropertyMetadata(null, (d, _) => ((DropDownButton)d)._session?.Refresh()));
    private readonly DropDownMenuSession _session;
    private bool _syncChecked;
    public DropDownButton()
    {
        _session = new(this, () => DropDownContextMenu, () => DropDownContextMenuDataContext ?? DataContext, SynchronizeChecked);
        Click += (_, _) =>
        {
            try
            {
                OnClick();
            }
            finally
            {
                SynchronizeChecked(_session.IsOpen);
            }
        };
        Unloaded += (_, _) => _session.Close();
        IsEnabledChanged += (_, _) =>
        {
            if (!IsEnabled)
                _session.Close();
        };
        DataContextChanged += (_, _) => _session.Refresh();
    }

    public MenuFlyout? DropDownContextMenu
    {
        get => (MenuFlyout?)GetValue(DropDownContextMenuProperty);
        set => SetValue(DropDownContextMenuProperty, value);
    }
    public object? DropDownContextMenuDataContext
    {
        get => GetValue(DropDownContextMenuDataContextProperty);
        set => SetValue(DropDownContextMenuDataContextProperty, value);
    }

    /// <summary>Shows the configured menu through the same lifetime as a native click.</summary>
    public void OpenDropDown() => _session.Open();
    /// <summary>Closes this trigger's current opening, not a menu subsequently owned by another trigger.</summary>
    public void CloseDropDown() => _session.Close();
    protected virtual void OnDropDownContextMenuChanged(DependencyPropertyChangedEventArgs e)
    {
    }

    private void MenuChanged(DependencyPropertyChangedEventArgs e)
    {
        // Cancel before the override can install or open a replacement menu.
        _session?.Close();
        OnDropDownContextMenuChanged(e);
    }

    protected virtual void OnClick()
    {
        if (_session.IsRequested)
            _session.Close();
        else
            _session.Open();
    }

    private void SynchronizeChecked(bool value)
    {
        if (_syncChecked || IsChecked == value)
            return;
        _syncChecked = true;
        try
        {
            IsChecked = value;
        }
        finally
        {
            _syncChecked = false;
        }

        // Checked callbacks may explicitly reject opening after the DP was set.
        if (value && IsChecked != true)
            _session.Close();
    }

    protected override void OnKeyDown(KeyRoutedEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Windows.System.VirtualKey.Escape && _session.IsRequested)
        {
            _session.Close();
            e.Handled = true;
        }
    }
}
