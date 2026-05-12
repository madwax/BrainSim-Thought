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
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.Threading.Tasks;
using UKS;

namespace BrainSimulator.Modules;

public partial class ModuleTextDlg : ModuleBaseDlg
{
    public ModuleTextDlg()
    {
        InitializeComponent();
    }

    public override bool Draw(bool checkDrawTimer)
    {
        if (!base.Draw(checkDrawTimer)) return false;
        //this has a timer so that no matter how often you might call draw, the dialog
        //only updates 10x per second
        ModuleText parent = (ModuleText)base.ParentModule;
        return true;
    }

    private void TheGrid_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        Draw(false);
    }

    private void BtnAdd_Click(object? sender, RoutedEventArgs e)
    {
        string phrase = tbPhrase.Text ?? string.Empty;

        tbPhrase.Text = string.Empty; tbPhrase.Focus();
        string message = ModuleText.AddText(phrase);
        SetStatus(message);
    }

    private async void btnBrowse_Click(object? sender, RoutedEventArgs e)
    {
        var openToPath = Utils.GetOrAddDocumentsSubFolder( Utils.UKSContentFolder );
        var filepathToWordFile = await Utils.OpenFileDialog( this, Utils.TitleBrainSimLoadWordList, Utils.FilterWordListFile, openToPath );

        if( string.IsNullOrEmpty( filepathToWordFile ) )
        {
            SetStatus( "User failed to select Word List File.", StatusMode.Error );
            return;
        }

        SetStatus( "Using Word List File: " + filepathToWordFile );
        txtFilePath.Text = filepathToWordFile;

        var module = ParentModule as ModuleText;
        module.CancelIncrementalLoad();
    }

    private async void btnLoad_Click(object? sender, RoutedEventArgs e)
    {
        string filePath = txtFilePath.Text?.Trim();

        if (string.IsNullOrWhiteSpace(filePath))
        {
            SetStatus("Please select a file first.", StatusMode.Warning);
            return;
        }

        if (!File.Exists(filePath))
        {
            SetStatus("File not found.", StatusMode.Error );
            return;
        }

        var module = ParentModule as ModuleText;
        if (module != null)
        {
            SetStatus("Loading phrases...");
            try
            {
                int count = await Task.Run(() => module.LoadTextFromFile(filePath));
                SetStatus($"Successfully loaded {count} phases(s) from file.");
            }
            catch (Exception ex)
            {
                SetStatus($"Error loading file: {ex.Message}", StatusMode.Error );
            }
        }
        else
        {
            SetStatus("Error: Module not found.", StatusMode.Error);
        }
    }

    private void btnTrigram_Click(object? sender, RoutedEventArgs e)
    {
        var module = ParentModule as ModuleText;
        if (module != null)
        {
            SetStatus("Generating trigrams...");
            int count = ModuleText.CreateTrigrams();
            SetStatus($"Successfully generated {count} trigrams.");
        }
        else
        {
            SetStatus("Error: Module not found.", StatusMode.Error );
        }
    }

    private void tbPhrase_PreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter)
        {
            BtnAdd_Click(null, null);
        }
    }
}