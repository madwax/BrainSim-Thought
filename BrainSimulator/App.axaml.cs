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
using BrainSimulator;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace BrainSimulator
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load( this );
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if( ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop )
            {
                desktop.Startup += this.OnStarted;
                desktop.Exit += this.OnExit;
            }
            base.OnFrameworkInitializationCompleted();
        }

        private void OnStarted( object sender, ControlledApplicationLifetimeStartupEventArgs e )
        {
            MainWindow mainWindow = new();
            mainWindow.Show();
        }

        private void OnExit( object sender, ControlledApplicationLifetimeExitEventArgs e )
        {
            // this.mainWindow.Close();
        }

        private static string startupString = "";

        public static string StartupString { get => startupString; set => startupString = value; }
    }
}
