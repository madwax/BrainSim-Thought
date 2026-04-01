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
using System.Windows;
using System.Windows.Controls;

namespace BrainSimulator.Modules;

public partial class ModuleWellBeingDlg : ModuleBaseDlg
{
    private bool _isUpdating;

    public ModuleWellBeingDlg()
    {
        InitializeComponent();
    }

    public override bool Draw(bool checkDrawTimer)
    {
        if (!base.Draw(checkDrawTimer)) return false;
        if (_isUpdating) return true;

        _isUpdating = true;
        WellBeingSlider.Value = ModuleWellBeing.State;
        _isUpdating = false;
        return true;
    }

    private void WellBeingSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdating) return;
        _isUpdating = true;
        ModuleWellBeing.SetState((float)e.NewValue);
        _isUpdating = false;
    }

    private void IncreaseButton_Click(object sender, RoutedEventArgs e)
    {
        ModuleWellBeing.Increase();
        Draw(false);
    }

    private void DecreaseButton_Click(object sender, RoutedEventArgs e)
    {
        ModuleWellBeing.Decrease();
        Draw(false);
    }

    private void TheGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        Draw(false);
    }
}