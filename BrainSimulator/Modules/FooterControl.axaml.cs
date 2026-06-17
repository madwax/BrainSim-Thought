using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace BrainSimulator;
public enum StatusModes
{
    Normal = 0,
    Warning,
    Error
};

public partial class FooterControl : UserControl
{
    private string moduleName = "";

    public FooterControl()
    {
        InitializeComponent();
    }
    public void SetModuleName(string name)
    {
        if( name.EndsWith( "Dlg" ) )
        {
            name = name.Substring( 0, name.Length - 3 );
        }
        moduleName = name;
    }

    public void SetStatus(string msg, StatusModes mode = StatusModes.Normal)
    {
        if( msg is null ) return;

        if( mode == StatusModes.Normal )
        {
            var t = msg.ToLower();
            if( t.Contains( "waring" ) )
            {
                mode = StatusModes.Warning;
            }
            if( t.Contains( "error" ) )
            {
                mode = StatusModes.Error;
            }
        }

        statusLabel.Classes.Clear();
        if( mode == StatusModes.Warning )
        {
            statusLabel.Classes.Add( "Warning" );
        }
        else if( mode == StatusModes.Error )
        {
            statusLabel.Classes.Add( "Error" );
        }
        statusLabel.Content = msg;
    }

    public string GetStatus()
    {
        return statusLabel.Content.ToString();
    }

    private void OnSourceButton( object sender, RoutedEventArgs e )
    {
        if( moduleName.Length == 0 ) return;
        MainWindow.theWindow.theCodeEditer.OpenAllSourcesInEditor( moduleName );
    }

    private void OnHelpButton( object sender, RoutedEventArgs e )
    {
        if( moduleName.Length == 0 ) return;
        ModuleDescriptionDlg md = new ModuleDescriptionDlg( moduleName );
        md.Show();
    }
}