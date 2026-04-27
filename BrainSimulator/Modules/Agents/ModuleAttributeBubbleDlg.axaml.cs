/*
 * Brain Simulator Through
 *
 * Copyright (c) 2026 Charles Simon
 *
 * This file is part of Brain Simulator Through and is licensed under
 * the MIT License. You may use, copy, modify, merge, publish, distribute,
 * sublicense, and/or sell copies of this software under the terms of
 * the MIT License.
 *
 * See the LICENSE file in the project root for full license information.
 */

using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;



namespace BrainSimulator.Modules
{
    public partial class ModuleAttributeBubbleDlg : ModuleBaseDlg, ISendDebugString
    {
        private List< string > pendingDebugMessages = new List< string >();

        public ModuleAttributeBubbleDlg()
        {
            InitializeComponent();
        }

        public void OnDebugString( string msg )
        {
            pendingDebugMessages.Add( msg );

            if( pendingDebugMessages.Count == 1 )
            {
                Dispatcher.UIThread.Post( () =>
                {
                    this.Draw( false );
                } );
            }
        }

        public override bool Draw(bool checkDrawTimer)
        {
            if (!base.Draw(checkDrawTimer)) return false;

            if( pendingDebugMessages.Count > 0 )
            {
                foreach( string item in pendingDebugMessages )
                {
                    DebugMessages.Items.Add( item );
                }

                // scroll to the bottom
                DebugMessageView.Offset = new Avalonia.Vector( DebugMessageView.Offset.X, DebugMessageView.Extent.Height - DebugMessageView.Viewport.Height );
            }
            return true;
        }

        private void TheGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Draw(false);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ModuleAttributeBubble parent = (ModuleAttributeBubble)base.ParentModule;
            parent.DoTheWork();
        }

        private void Enable_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb)
            {
                ModuleAttributeBubble parent = (ModuleAttributeBubble)base.ParentModule;
                if (parent is not null)
                    parent.isEnabled = cb.IsChecked == true;
            }
        }
    }
}
