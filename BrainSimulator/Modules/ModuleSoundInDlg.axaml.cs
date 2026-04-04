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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UKS;

namespace BrainSimulator.Modules;

public partial class ModuleSoundInDlg : ModuleBaseDlg
{
    public ModuleSoundInDlg()
    {
        InitializeComponent();
    }

    public override bool Draw(bool checkDrawTimer)
    {
        if (!base.Draw(checkDrawTimer)) return false;
        //this has a timer so that no matter how often you might call draw, the dialog
        //only updates 10x per second
        ModuleSoundIn parent = (ModuleSoundIn)base.ParentModule;
        return true;
    }

    private void TheGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        Draw(false);
    }


    private void Dlg_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.IsRepeat) return;
        if (sender is not ModuleSoundInDlg dlg) return;
        if (ParentModule is not ModuleSoundIn module) return;
        switch (e.Key)
        {
            case Key.Z: module.StartNote(60); break; // C4
            case Key.S: module.StartNote(61); break; // C#4
            case Key.X: module.StartNote(62); break; // D4
            case Key.D: module.StartNote(63); break; // D#4
            case Key.C: module.StartNote(64); break; // E4
            case Key.V: module.StartNote(65); break; // F4
            case Key.G: module.StartNote(66); break; // F#4
            case Key.B: module.StartNote(67); break; // G4
            case Key.H: module.StartNote(68); break; // G#4
            case Key.N: module.StartNote(69); break; // A4
            case Key.J: module.StartNote(70); break; // A#4
            case Key.M: module.StartNote(71); break; // B4
            case Key.OemComma: module.StartNote(72); break; // C5
            case Key.L: module.StartNote(73); break; // C#5
            case Key.OemPeriod: module.StartNote(74); break; // D5
            case Key.OemSemicolon: module.StartNote(75); break; // D#5
            case Key.OemQuestion: module.StartNote(76); break; // E5
        }
    }

    private void Dlg_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (e.IsRepeat) return;
        if (sender is not ModuleSoundInDlg dlg) return;
        if (ParentModule is not ModuleSoundIn module) return;
        switch (e.Key)
        {
            case Key.Z: module.StopNote(60); break;
            case Key.S: module.StopNote(61); break;
            case Key.X: module.StopNote(62); break;
            case Key.D: module.StopNote(63); break;
            case Key.C: module.StopNote(64); break;
            case Key.V: module.StopNote(65); break;
            case Key.G: module.StopNote(66); break;
            case Key.B: module.StopNote(67); break;
            case Key.H: module.StopNote(68); break;
            case Key.N: module.StopNote(69); break;
            case Key.J: module.StopNote(70); break;
            case Key.M: module.StopNote(71); break;
            case Key.OemComma: module.StopNote(72); break;
            case Key.L: module.StopNote(73); break;
            case Key.OemPeriod: module.StopNote(74); break;
            case Key.OemSemicolon: module.StopNote(75); break;
            case Key.OemQuestion: module.StopNote(76); break;
        }
    }

    /*                    "C" => 60,
                    "D" => 62,
                    "E" => 64,
                    "F" => 65,
                    "G" => 67,
                    "A" => 69,
                    "B" => 71,
                    "C+" => 72,
    */
}

