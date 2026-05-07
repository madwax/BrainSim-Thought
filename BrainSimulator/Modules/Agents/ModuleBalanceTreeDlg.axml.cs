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
 

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace BrainSimulator.Modules
{
    public partial class ModuleBalanceTreeDlg : ModuleBaseDlg
    {
        public ModuleBalanceTreeDlg()
        {
            InitializeComponent();
            this.Loaded += OnEnableDebugStream;
        }

        public override bool Draw(bool checkDrawTimer)
        {
            if (!base.Draw(checkDrawTimer)) return false;

            this.DrawDebugStrings( DebugMessages, DebugMessageView );
            return true;
        }

        private void TheGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Draw(false);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ModuleBalanceTree parent = (ModuleBalanceTree)base.ParentModule;
            parent.DoTheWork();
        }

        private void Enable_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb)
            {
                ModuleBalanceTree parent = (ModuleBalanceTree)base.ParentModule;
                if (parent is not null)
                    parent.isEnabled = cb.IsChecked == true;
            }
        }
    }
}
