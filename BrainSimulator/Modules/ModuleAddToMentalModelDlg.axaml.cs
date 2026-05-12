using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

using UKS;

namespace BrainSimulator.Modules;

public partial class ModuleAddToMentalModelDlg : ModuleBaseDlg
{
    private const int HistoryLimit = 8;
    private readonly List<string> _history = new();
    private bool _isTextChangingInternally;

    public ModuleAddToMentalModelDlg()
    {
        InitializeComponent();
    }

    public override bool Draw( bool checkDrawTimer )
    {
        if( !base.Draw( checkDrawTimer ) ) return false;
        return true;
    }

    private void ThoughtBox_Loaded( object sender, RoutedEventArgs e )
    {
        LoadHistory( ( ParentModule as ModuleBase )?.GetSavedDlgAttribute( "ThoughtHistory" ) );
        UpdateHistoryItems();
    }

    private void ThoughtBox_KeyDown( object? sender, KeyEventArgs e )
    {
        if( sender is not TextBox tb ) return;

        if( e.Key == Key.Enter )
        {
            AddToHistory( tb.Text );

            // Port Note - I thing the original code was to clear the selection
            tb.SelectionStart = 0;
            tb.SelectionEnd = 0;

            e.Handled = true;
        }
    }

    private void ThoughtBox_TextChanged( object? sender, TextChangedEventArgs e )
    {
        if( _isTextChangingInternally ) return;
        if( sender is not TextBox tb ) return;

        string searchText = tb.Text ?? string.Empty;
        if( !string.IsNullOrEmpty( searchText ) )
        {
            var suggestion = ThoughtLabels.LabelList.Keys
                .Where( key => key.StartsWith( searchText, StringComparison.OrdinalIgnoreCase ) )
                .OrderBy( key => key )
                .FirstOrDefault();

            if( suggestion is not null )
                suggestion = ThoughtLabels.GetThought( suggestion )?.Label ?? suggestion;

            if( suggestion is not null && !suggestion.Equals( searchText, StringComparison.OrdinalIgnoreCase ) )
            {
                int caret = tb.CaretIndex;
                _isTextChangingInternally = true;
                tb.Text = suggestion;

                tb.CaretIndex = caret;
                tb.SelectionStart = caret;
                tb.SelectionEnd = suggestion.Length - caret;

                _isTextChangingInternally = false;
            }
        }
    }
    private void ThoughtBoxHistory_Clicked( object? sender, RoutedEventArgs e )
    {
        ThoughtBoxHistory_Current.Open();
    }

    private void ThoughtBoxHistory_OnMenuItem( object? sender, RoutedEventArgs e )
    {
        if( sender is null || sender is not MenuItem )
        {
            return;
        }

        var menuItemIs = sender as MenuItem;

        ThoughtBox.Text = menuItemIs.Header.ToString();

        Draw( false );
    }


    private void AddToHistory( string value )
    {
        if( string.IsNullOrWhiteSpace( value ) ) return;
        int existing = _history.FindIndex( x => string.Equals( x, value, StringComparison.OrdinalIgnoreCase ) );
        if( existing >= 0 ) _history.RemoveAt( existing );
        _history.Insert( 0, value );
        if( _history.Count > HistoryLimit )
            _history.RemoveRange( HistoryLimit, _history.Count - HistoryLimit );

        UpdateHistoryItems();
        ( ParentModule as ModuleBase )?.SetSavedDlgAttribute( "ThoughtHistory", string.Join( "|", _history ) );
    }

    private void LoadHistory( string raw )
    {
        _history.Clear();
        if( !string.IsNullOrWhiteSpace( raw ) )
            _history.AddRange( raw.Split( '|' ).Where( x => !string.IsNullOrWhiteSpace( x ) ) );
    }

    private void UpdateHistoryItems()
    {
        _isTextChangingInternally = true;

        ThoughtBoxHistory_Current.Items.Clear();

        foreach( var item in _history )
        {
            var menuItem = new MenuItem
            {
                Header = item
            };
            menuItem.Click += this.ThoughtBoxHistory_OnMenuItem;
            ThoughtBoxHistory_Current.Items.Add( menuItem );
        }
        _isTextChangingInternally = false;
    }

    private void Ok_Click( object sender, RoutedEventArgs e )
    {
        if( ParentModule is not ModuleAddToMentalModel mod ) return;

        string label = ThoughtBox.Text?.Trim();
        if( !double.TryParse( AzimuthBox.Text, out double az ) ) az = 0;
        if( !double.TryParse( ElevationBox.Text, out double el ) ) el = 0;
        if( !double.TryParse( DistanceBox.Text, out double dist ) ) dist = 1;

        mod.AddThoughtToMentalModel( label, az, el, dist );
        AddToHistory( label );
    }


}