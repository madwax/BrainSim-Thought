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

using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using BrainSimulator.Modules;

namespace BrainSimulator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private async void buttonSave_Click(object sender, RoutedEventArgs e)
        {
            if (currentFileName.Length == 0)
            {
                buttonSaveAs_Click(null, null);
            }
            else
            {
                SaveFile(currentFileName);
            }
        }

        private async void buttonSaveAs_Click(object sender, RoutedEventArgs e)
        {
            if ( await SaveAs())
            {
                SaveButton.IsEnabled = true;
            }
        }

        private async void buttonReloadNetwork_click(object sender, RoutedEventArgs e)
        {
            if ( ! await PromptToSaveChanges())
                return;

            if (currentFileName != "")
            {
                LoadCurrentFile();
                ShowAllModuleDialogs();
            }
        }

        private void button_Exit_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.Save();
            CloseAllModuleDialogs();
            CloseAllModules();
            this.Close();
        }


        public ModuleBase CreateNewModule(string moduleTypeLabel, string moduleLabel = "")
        {
            Type t = Type.GetType("BrainSimulator.Modules." + moduleTypeLabel);
            if (t is null) 
                return null;
            ModuleBase theModule = (Modules.ModuleBase)Activator.CreateInstance(t);

            theModule.Label = moduleLabel;
            if (moduleLabel == "")
                theModule.Label = moduleTypeLabel;
            theModule.GetUKS();
            return theModule;
        }
        private void ModuleListComboBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is ComboBox cb)
                if (e.Key != Key.Enter && e.Key != Key.Escape)
                    cb.IsDropDownOpen = true;
        }

        private void ModuleListComboBox_DropDownClosed(object sender, EventArgs e)
        {
            if (sender is ComboBox cb)
            {
                if (cb.SelectedItem is not null)
                {

//                    string moduleName = ((Label)cb.SelectedItem).Content.ToString();
                    string moduleName = cb.SelectedItem.ToString();
                    cb.SelectedIndex = -1;
                    ActivateModule(moduleName);
                }
            }

            ReloadActiveModulesSP();

        }
        private async void button_FileNew_Click(object sender, RoutedEventArgs e)
        {
            if ( ! await PromptToSaveChanges())
                return;

            SuspendEngine();
            CloseAllModuleDialogs();
            CloseAllModules();
            UnloadActiveModules();

            CreateEmptyUKS(); // to avoid keeping too many bytes occupied...

            currentFileName = "";
            SetCurrentFileNameToProperties();

            LoadModuleTypeMenu();

            InitializeActiveModules();

            LoadMRUMenu();

            SetTitleBar();

            ResumeEngine();
        }

        private async void buttonLoad_Click(object sender, RoutedEventArgs e)
        {
            if (! await PromptToSaveChanges())
                return;
            string fileName = "_Open";
            if (sender is MenuItem mainMenu)
                fileName = (string)mainMenu.Header;

            if (fileName == "_Open")
            {
                var openStartPath = Utils.GetOrAddDocumentsSubFolder( Utils.UKSContentFolder );
                var filepathToOpen = await Utils.OpenFileDialog( this, Utils.TitleUKSFileSave, Utils.FilterXMLs, openStartPath );
                if( filepathToOpen is not null )
                {
                    currentFileName = filepathToOpen;
                    LoadCurrentFile();
                }
            }
            else
            {
                if (sender is MenuItem mi)
                {
                    //this is a file name from the File menu
                    currentFileName = ToolTip.GetTip( mi ).ToString(); //Path.GetFullPath("./UKSContent/" + fileName + ".xml");
                    LoadCurrentFile();
                }
            }
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (! await PromptToSaveChanges())
                return;
            CloseAllModuleDialogs();
            CloseAllModules();
            moduleHandler.ClosePythonEngine();
        }
    }
}
