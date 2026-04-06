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
using Avalonia.Media;

using BrainSimulator.Modules;
using System;
using System.Collections;
using System.Collections.Specialized;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using UKS;

namespace BrainSimulator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static StackPanel loadedModulesSP;
        private bool LoadFile(string fileName)
        {
            SuspendEngine();
            CloseAllModuleDialogs();
            UnloadActiveModules();

            if (!theUKS.LoadUKSfromXMLFile(fileName))
            {
                theUKS = new UKS.UKS();
                return false;
            }
            currentFileName = fileName;

            if (theUKS.Labeled("BrainSim") is null)
                CreateEmptyUKS();

            SetCurrentFileNameToProperties();

            UpdateModuleListsInUKS();
            LoadActiveModules();
            ReloadActiveModulesSP();
            ShowAllModuleDialogs();
            SetTitleBar();
            ResumeEngine();
            AddFileToMRUList(fileName); 
            return true;
        }

        public void ReloadActiveModulesSP()
        {
            ActiveModuleSP.Children.Clear();

            Thought activeModuleParent = theUKS.Labeled("ActiveModule");
            //TODO: Remove
            activeModuleParent.AddParent("BrainSim");

            if (activeModuleParent is null) { return; }
            var activeModules1 = activeModuleParent.Children;
            activeModules1 = activeModules1.OrderBy(x => x.Label).ToList();

            foreach (Thought t in activeModules1)
            {
                //what kind of module is this?
                Thought t1 = t.Parents.FindFirst(x => x.HasAncestor("AvailableModule"));
                if (t1 is null) continue;
                string moduleType = t1.Label;

                TextBlock tb = new TextBlock();
                tb.Text = t.Label;
                tb.Margin = new Thickness(5, 2, 5, 2);
                tb.Padding = new Thickness(10, 3, 10, 3);
                tb.ContextMenu = new ContextMenu();
                if (moduleType.Contains(".py"))
                { }
                else
                {
                    ModuleBase mod = activeModules.FindFirst(x => x.Label == t.Label);
                    CreateContextMenu(mod, tb, tb.ContextMenu);
                }
                tb.Background = new SolidColorBrush(Colors.LightGreen);
                ActiveModuleSP.Children.Add(tb);
            }
        }
        void UnloadActiveModules()
        {
            Thought activeModulesParent = theUKS.Labeled("ActiveModule");
            if (activeModulesParent is null) return;
            var activeModules1 = activeModulesParent.Children;

            foreach (Thought t in activeModules1)
            {
                for (int i = 0; i < t.LinksTo.Count; i++)
                {
                    Link r = t.LinksTo[i];
                    t.RemoveLink(r);
                    r.To.Delete();
                }
                t.Delete();
            }
        }

        void LoadActiveModules()
        {
            activeModules.Clear();
#if PYTHON_SUPPORT
            pythonModules.Clear();
#endif
            moduleHandler.ClearAllPythonModules();

            var activeModules1 = theUKS.Labeled("ActiveModule").Children;
            activeModules1 = activeModules1.OrderBy(x => x.Label).ToList();

            foreach (Thought t in activeModules1)
            {
                //what kind of module is this?
                Thought tModuleType = t.Parents.FindFirst(x => x.HasAncestor("AvailableModule"));
                if (tModuleType is null) continue;
                string moduleType = tModuleType.Label;

                if (moduleType.Contains(".py"))
                {
#if PYTON_SUPPORT
                    pythonModules.Add(t.Label);
#endif
                }
                else
                {
                    ModuleBase mod = CreateNewModule(moduleType, t.Label);
                    if (mod is not null) 
                        activeModules.Add(mod);
                    else
                    {
                        theUKS.Labeled("ActiveModule").RemoveChild(t);
                    }
                }
            }
        }
        private bool SaveFile(string fileName)
        {
            Save();
            AddFileToMRUList(fileName);
            return true;
        }

        private void AddFileToMRUList(string filePath)
        {
            StringCollection MRUList = (StringCollection)Properties.Settings.Default["MRUList"];
            if (MRUList is null)
                MRUList = new StringCollection();
            MRUList.Remove(filePath); //remove it if it's already there
            MRUList.Insert(0, filePath); //add it to the top of the list
            Properties.Settings.Default["MRUList"] = MRUList;
            Properties.Settings.Default.Save();
            LoadMRUMenu();
        }
        public static void RemoveFileFromMRUList(string filePath)
        {
            StringCollection MRUList = (StringCollection)Properties.Settings.Default["MRUList"];
            if (MRUList is null)
                MRUList = new StringCollection();
            MRUList.Remove(filePath); //remove it if it's already there
            Properties.Settings.Default["MRUList"] = MRUList;
            Properties.Settings.Default.Save();
        }
        private void LoadMRUMenu()
        {
            /*
            MRUListMenu.Items.Clear();
            StringCollection MRUList = (StringCollection)Properties.Settings.Default["MRUList"];
            if (MRUList is null)
                MRUList = new StringCollection();
            foreach (string fileItem in MRUList)
            {
                if (fileItem is null) continue;
                string shortName = Path.GetFileNameWithoutExtension(fileItem);
                MenuItem mi = new MenuItem() { Header = shortName };
                mi.Click += buttonLoad_Click;
                ToolTip.SetTip( mi, fileItem );
                MRUListMenu.Items.Add(mi);
            }*/
        }

        private void LoadCurrentFile()
        {
            LoadFile(currentFileName);
        }

        private static void SetCurrentFileNameToProperties()
        {
            Properties.Settings.Default["CurrentFile"] = currentFileName;
            Properties.Settings.Default.Save();
        }

        private async Task<bool> PromptToSaveChanges()
        {
            var result = await MessageBox.YesNoCancel("Save current UKS content first?", "Save?" );
            if (result == MessageBox.Buttons.Cancel)
                return false;
            if (result == MessageBox.Buttons.Yes)
                return Save();
            return true;
        }

        private bool Save()
        {
            SetupBeforeSave();
            return theUKS.SaveUKStoXMLFile(currentFileName);
        }
        private async Task<bool> SaveAs()
        {
            var saveToStartPath = Utils.GetOrAddDocumentsSubFolder( Utils.UKSContentFolder );
            var filepathToSaveTo = await Utils.SaveFileDialog( this, Utils.TitleUKSFileSave, Utils.FilterXMLs, saveToStartPath );
            if( filepathToSaveTo == null) return false;

            MainWindow.SuspendEngine();

            currentFileName = filepathToSaveTo;

            theUKS.SaveUKStoXMLFile(currentFileName);

            AddFileToMRUList( currentFileName );
            SetCurrentFileNameToProperties();

            SetTitleBar();

            MainWindow.ResumeEngine();
            return true;
        }
    }
}
