/*
 * Brain Simulator Thought
 *
 * Copyright (c) 2026 Charles Simon
 *
 * This file is part of Brain Simulator Thought and is licensed under
 * the MIT License. You may use, copy, modify, merge, publish, distribute,
 * sublicense, and/or sell copies of this software under the terms of
 * the MIT License.
 *
 * See the LICENSE file in the project root for full license information.
 */


using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Layout;
using Avalonia.Media;

namespace BrainSimulator.Modules;

public class ModuleBaseDlg : Window
{
    public ModuleBase ParentModule;

#if TO_REMOVE
    private DateTime dt;
    private DispatcherTimer timer;  // It's not used.
    public int UpdateMS = 100;
#endif

    public Label statusLabel;
    private bool initializedLayout = false;

    public ModuleBaseDlg()
    {
        this.Loaded += ModuleBaseDlg_Loaded;
    }

    private void ModuleBaseDlg_Loaded(object sender, RoutedEventArgs e)
    {
        if (initializedLayout) return;
        initializedLayout = true;

        // capture original content
        Control? originalContent = this.Content as Control;

        DockPanel shell = new()
        {
            Name="dialogShell",
            LastChildFill=true
        };

        Grid bottomBar = new()
        {
            Name = "bottomBar"
        };
        bottomBar.ColumnDefinitions.Add( new ColumnDefinition { Width = new GridLength( 1, GridUnitType.Star ) } ); // status stretch
        bottomBar.ColumnDefinitions.Add( new ColumnDefinition { Width = new GridLength( 45 ) } );                    // src button
        bottomBar.ColumnDefinitions.Add( new ColumnDefinition { Width = new GridLength( 45 ) } );                    // help button

        statusLabel = new()
        {
            Content = "OK",
            Name = "statusLabel"
        };

        Button sourceButton = new Button
        {
            Content = "src",
            Name = "sourceButton"
        };
        sourceButton.Click += SourceButton_Click;
        ToolTip.SetTip( sourceButton, "Show dialog sources" );

        Button helpButton = new Button
        {
            Content = "?",
            Name = "helpButton"
        };
        helpButton.Click += HelpButton_Click;
        ToolTip.SetTip( helpButton, "Show dialog help" );

        Grid.SetColumn( statusLabel, 0 );
        bottomBar.Children.Add( statusLabel );

        Grid.SetColumn( sourceButton, 1 );
        bottomBar.Children.Add( sourceButton );

        Grid.SetColumn( helpButton, 2 );
        bottomBar.Children.Add( helpButton );

        Border borderFooter = new()
        {
            Name = "dialogShellFooter"
        };
        DockPanel.SetDock( borderFooter, Dock.Bottom );

        borderFooter.Child = bottomBar;

        shell.Children.Add( borderFooter );

        if( originalContent is not null )
        {
            this.Content = null; // detach

            Border borderContent = new()
            {
                Name = "dialogShellContent"
            };

            borderContent.Margin = this.Margin;
            borderContent.Child = originalContent;

            DockPanel.SetDock( borderContent, Dock.Top );
            shell.Children.Add( borderContent );
        }
        else
        {
            // RHC - should put a label saying No Content
            Label emptyness = new()
            {
                Name = "dialogShellNoContent",
                Content = "No dialog content currently defined"
            };

            DockPanel.SetDock( emptyness, Dock.Top );
            shell.Children.Add( emptyness );
        }

        // set new content
        this.Content = shell;
    }

#if TO_REMOVE
    /// <summary>
    /// Searches for a file with the given name (no path) in the specified root directory
    /// and all its subdirectories.
    /// </summary>
    public static string? FindFile(string rootPath, string fileName)
    {
        if (!Directory.Exists(rootPath))
            throw new DirectoryNotFoundException($"Root path not found: {rootPath}");

        return Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories)
                        .FirstOrDefault(f => string.Equals(
                            Path.GetFileName(f),
                            fileName,
                            StringComparison.OrdinalIgnoreCase));
    }
#endif 

    private void SourceButton_Click(object sender, RoutedEventArgs e)
    {
        string theModuleType = this.GetType().Name.ToString();
        MainWindow.theWindow.theCodeEditer.OpenAllSourcesInEditor( theModuleType );
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        string theModuleType = this.GetType().Name.ToString();
        theModuleType = theModuleType.Replace("Dlg", "");
        ModuleDescriptionDlg md = new ModuleDescriptionDlg(theModuleType);
        md.Show();
    }

    virtual public bool Draw(bool checkDrawTimer)
    {
        return true;
    }

#if TO_REMOVE
    public void Timer_Tick(object sender, EventArgs e)
    {
        timer.Stop();
        if (Application.Current is null) return;
        if (this is not null)
            Draw(false);

    }

    //this picks up a final draw after 1/4 second 
    public void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
    {
        timer.Stop();
        if (Application.Current is null) return;
        if (this is not null)
            Draw(false);
    }
#endif

    /// <summary>
    /// Sets a status message at the bottom of the dialog. Seets the background yellow if the color is red or null
    /// </summary>
    /// <param name="message"></param>
    /// <param name="c">Defaults to red</param>
    public void SetStatus(string message, Color? c = null)
    {
        if (c is null) c = Colors.Red;
        statusLabel.Background = new SolidColorBrush(Colors.Gray);
        if (c == Colors.Red && (message != "OK" && message != "" ))
            statusLabel.Background = new SolidColorBrush(Colors.LemonChiffon);
        if (message == "OK" || message == "")
            statusLabel.Foreground = new SolidColorBrush(Colors.Black);
        else
            statusLabel.Foreground = new SolidColorBrush((Color)c);

        statusLabel.Content = message;
    }
    public string GetStatus()
    {
        return statusLabel.Content.ToString();
    }
}
