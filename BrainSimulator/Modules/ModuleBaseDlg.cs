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

        statusLabel = new()
        {
            Content = "OK",
            Name = "ReportStatus"
        };

        Button sourceButton = new Button
        {
            Content = "src",
            Name = "footerSource"
        };
        sourceButton.Click += SourceButton_Click;
        ToolTip.SetTip( sourceButton, "Show dialog sources" );

        Button helpButton = new Button
        {
            Content = "?",
            Name = "footerHelp"
        };
        helpButton.Click += HelpButton_Click;
        ToolTip.SetTip( helpButton, "Show dialog help" );

        // build the footer area up.

        Grid bottomBar = new();
        bottomBar.Classes.Add( "Footer" );

        bottomBar.ColumnDefinitions.Add( new ColumnDefinition { Width = new GridLength( 1, GridUnitType.Star ) } ); // status stretch
        bottomBar.ColumnDefinitions.Add( new ColumnDefinition { Width = new GridLength( 45 ) } );                    // src button
        bottomBar.ColumnDefinitions.Add( new ColumnDefinition { Width = new GridLength( 45 ) } );                    // help button

        Grid.SetColumn( statusLabel, 0 );
        bottomBar.Children.Add( statusLabel );

        Grid.SetColumn( sourceButton, 1 );
        bottomBar.Children.Add( sourceButton );

        Grid.SetColumn( helpButton, 2 );
        bottomBar.Children.Add( helpButton );

        Border borderFooter = new();
        DockPanel.SetDock( borderFooter, Dock.Bottom );
        borderFooter.Child = bottomBar;
        borderFooter.Classes.Add( "Footer" );

        DockPanel shell = new()
        {
            LastChildFill = true
        };
        shell.Classes.Add( "Container" );
        shell.Children.Add( borderFooter );

        Border borderContent = new();
        DockPanel.SetDock( borderContent, Dock.Top );

        if( originalContent is not null )
        {
            this.Content = null; // detach

            borderContent.Margin = this.Margin;
            borderContent.Child = originalContent;
            borderContent.Classes.Add( "Content" );
        }
        else
        {
            // RHC - should put a label saying No Content
            Label emptyness = new()
            {
                Content = "No dialog content currently defined"
            };
            borderContent.Child = emptyness;
            borderContent.Classes.Add( "Emptyness" );
        }
        shell.Children.Add( borderContent );

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
    /// Defines the background/foreground color of a message being displayed in the status area of the footer
    /// </summary>
    public enum StatusMode
    {
        Normal = 0,
        Warning, 
        Error
    };

    /// <summary>
    /// Sets a status message at the bottom of the dialog.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="mode">Defines what type of message classification we have to display, see enum StatuMode</param>
    public void SetStatus(string message, StatusMode mode = StatusMode.Normal)
    {
        statusLabel.Classes.Clear();
        if( mode == StatusMode.Warning )
        {
            statusLabel.Classes.Add( "Warning" );
        }
        else if( mode == StatusMode.Error )
        {
            statusLabel.Classes.Add( "Error" );
        }
        statusLabel.Content = message;
    }
    public string GetStatus()
    {
        return statusLabel.Content.ToString();
    }
}
