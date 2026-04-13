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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using BrainSimulator.Modules;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using UKS;
using static System.Configuration.ConfigurationManager;


// To Be fixed later on.
// SUPPORT_CODEEDIT - Opening up the source code in your editor.

namespace BrainSimulator
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static readonly StyledProperty<string> moduleNameProperty = AvaloniaProperty.Register<MenuItem, string>( "moduleName" );

        //TODO move these to ModuleHandler
        public List<ModuleBase> activeModules = new();

#if PYTHON_SUPPORT
        public List<string> pythonModules = new();
        public static string pythonPath = "";
#endif
        private static DispatcherTimer timer = null;
        public static MainWindow theWindow = null;
        public static ModuleHandler moduleHandler = new();
        public static UKS.UKS theUKS = moduleHandler.theUKS;
        private static StackPanel loadedModulesSP;

        private bool supportCodeEditing = false;

        private string CurrentFileName
        {
            get 
            {
                return Properties.Settings.Default[ "CurrentFile" ] as string;
            }
            set
            {
                // Update the title 
                Title = "Brain Simulator Thought " + System.IO.Path.GetFileNameWithoutExtension( value );

                // Add it to the MRUList
                if(value.Length > 0)
                {
                    AddFileToMRUList( value );
                }

                // And save it back to disk
                Properties.Settings.Default[ "CurrentFile" ] = value;
                Properties.Settings.Default.Save();
            }
        }

        public MainWindow()
        {
            theWindow = this;

            InitializeComponent();

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;

            // set a local flag so we don't render code editing stuff if we can't do it.
            supportCodeEditing = Utils.PlatformSupportCodeEditing();
        }

        // Support Dialogs
        private async Task<string> SaveAsDialog()
        {
            var saveToStartPath = Utils.GetOrAddDocumentsSubFolder( Utils.UKSContentFolder );
            var filePathToSaveTo = await Utils.SaveFileDialog( this, Utils.TitleUKSFileSave, Utils.FilterXMLs, saveToStartPath );
            if( filePathToSaveTo == null )
            {
                return "";
            }
            return filePathToSaveTo;
        }

        // Event Handlers below

        private void MainWindow_Loaded( object sender, RoutedEventArgs e )
        {
            // Setup the timer.
            if( timer == null )
            {
                timer = new DispatcherTimer();
            }
            timer.Interval = TimeSpan.FromSeconds( 0.001 );
            timer.Tick += Dt_Tick;

            // Get the paths and stuff needed to run python code.
            SetupPythonEnv();

            // Populate the MRU list which what we have on properties.
            LoadMRUMenu();

            // Load last file
            string fileName = CurrentFileName;
            if( fileName.Length > 0 )
            {
                try
                {
                    if( this.LoadFile( fileName ) )
                    {
                        return;
                    }
                    else
                    {
                        MessageBox.Alert( "Failed to load UKS file: " + CurrentFileName + ". Using defaults", "Application StartUp" );
                    }
                }
                catch( Exception ex )
                {
                    MessageBox.Alert( "Failed to load UKS file: " + CurrentFileName + " Exception: " + ex.ToString(), "Application StartUp" );
                }
            }

            // Need to use the default
            this.NewFile();
        }

        private async void MainWindow_Closing( object sender, WindowClosingEventArgs e )
        {
        }

        private async void MainMenu_FileNew( object sender, RoutedEventArgs e )
        {
            if( this.CanSaveCurrentUKS() == true )
            {
                var button = await MessageBox.YesNoCancel( "Would you like to save the exiting UKS to disk?", "New UKS" );
                if( button == MessageBox.Buttons.Cancel )
                {
                    // abort
                    return;
                }
                if( button == MessageBox.Buttons.Yes )
                {
                    var filepath = await this.SaveAsDialog();
                    if( filepath.Length > 0 )
                    {
                        this.SaveFile( filepath );
                    }
                }
            }
            this.NewFile();
        }

        private async void MainMenu_FileOpen( object sender, RoutedEventArgs e )
        {
            if( this.CanSaveCurrentUKS() == true )
            {
                var button = await MessageBox.YesNoCancel( "Would you like to save the exiting UKS to disk?", "New UKS" );
                if( button == MessageBox.Buttons.Cancel )
                {
                    // abort
                    return;
                }
                if( button == MessageBox.Buttons.Yes )
                {
                    var filepath = await this.SaveAsDialog();
                    if( filepath.Length > 0 )
                    {
                        this.SaveFile( filepath );
                    }
                }
            }

            string fileName = "_Open";
            if( sender is MenuItem mainMenu )
                fileName = ( string )mainMenu.Header;

            if( fileName == "_Open" )
            {
                var openStartPath = Utils.GetOrAddDocumentsSubFolder( Utils.UKSContentFolder );
                var filepathToOpen = await Utils.OpenFileDialog( this, Utils.TitleUKSFileSave, Utils.FilterXMLs, openStartPath );
                if( filepathToOpen is not null )
                {
                    this.LoadFile( filepathToOpen );
                }
            }
            else
            {
                if( sender is MenuItem mi )
                {
                    //this is a file name from the File menu
                    var filepathToOpen = ToolTip.GetTip( mi ).ToString(); //Path.GetFullPath("./UKSContent/" + fileName + ".xml");
                    this.LoadFile( filepathToOpen );
                }
            }
        }

        private async void MainMenu_FileExit( object sender, RoutedEventArgs e )
        {
            if( CanSaveCurrentUKS() )
            {
                var button = await MessageBox.YesNoCancel( "Would you like Save current UKS on exit?", "Application Closing" );
                if( button == MessageBox.Buttons.Yes )
                {
                    var path = await this.SaveAsDialog();
                    if( path.Length > 0 )
                    {
                        this.SaveFile( path );
                    }
                }
                else if( button == MessageBox.Buttons.Cancel )
                {
                    // it's an abort
                    return;
                }
            }

            if( timer != null )
            {
                timer.Stop();
                timer = null;
            }

            Properties.Settings.Default.Save();
            CloseAllModuleDialogs();
            CloseAllModules();

            moduleHandler.ClosePythonEngine();

            this.Close();
        }

        private async void MainMenu_FileSave( object sender, RoutedEventArgs e )
        {
            var filepath = CurrentFileName;
            if( filepath.Length == 0 )
            {
                filepath = await this.SaveAsDialog();
            }

            if( filepath.Length == 0 )
            {
                MessageBox.Alert( "No file selected", "Save File" );
                return;
            }

            if( SaveFile( filepath ) == true )
            {
                MessageBox.Alert( "Failed to save to file: " + filepath, "Save File" );
            }
        }

        private async void MainMenu_FileSaveAs( object sender, RoutedEventArgs e )
        {
            var filepath = await this.SaveAsDialog();
            if( filepath.Length == 0 )
            {
                // User didn't want to save
                return;
            }

            if( SaveFile( filepath ) == true )
            {
                SaveButton.IsEnabled = true;
            }
            else
            {
                MessageBox.Alert( "Failed to save to file: " + filepath, "Save As File" );
            }
        }

        private async void ActiveModulesSP_ContextMenu_Click( object sender, RoutedEventArgs e )
        {
            //Handle delete  & initialize commands
            if( sender is MenuItem mi && mi is not null && mi.Parent is not null )
            {
                string moduleName = ( string )mi.Parent.GetValue( moduleNameProperty );
                if( moduleName.Length > 0 )
                {
                    ModuleBase? m = activeModules.FindFirst( x => x.Label == moduleName );
                    if( m is not null )
                    {
                        int indexToModule = activeModules.IndexOf( m );

                        switch( ( string )mi.Header )
                        {
                            case "View Source":
                            case "View Dialog Source":
                            {
                                // should never get here but...
                                if( supportCodeEditing == false ) return;
#if SUPPORT_CODEEDIT
                                string theModuleType = m.GetType().Name.ToString();

                                if( ( string )mi.Header == "View Dialog Source" )
                                    theModuleType += "Dlg.xaml";

                                string cwd = System.IO.Directory.GetCurrentDirectory();
                                if( cwd.Contains( "bin\\" ) )
                                    cwd = cwd.ToLower().Substring( 0, cwd.IndexOf( "bin\\" ) );
                                string fileName = cwd + @"modules\" + theModuleType + ".cs";
                                if( File.Exists( fileName ) )
                                    OpenSource( fileName );
                                else
                                {
                                    fileName = cwd + @"BrainSim2modules\" + theModuleType + ".cs";
                                    OpenSource( fileName );
                                }
#endif
                                break;
                            }

                            case "Delete":
                            {
                                if( indexToModule >= 0 )
                                {
                                    DeleteModule( moduleName );
                                }
                                break;
                            }

                            case "Initialize":
                            {
                                if( indexToModule >= 0 )
                                {
                                    try
                                    {
                                        activeModules[ indexToModule ].Initialize();
                                    }
                                    catch( Exception e1 )
                                    {
                                        MessageBox.Alert( "Initialize failed on module " + activeModules[ indexToModule ].Label + ".   Message: " + e1.Message, "Error" );
                                    }
                                }
                                break;
                            }
                            case "Show Dialog":
                            {
                                if( indexToModule >= 0 )
                                {
                                    activeModules[ indexToModule ].OpenDlg();
                                }
                                break;
                            }
                            case "Hide Dialog":
                            {
                                if( indexToModule >= 0 )
                                {
                                    activeModules[ indexToModule ].CloseDlg();
                                }
                                break;
                            }
                            case "Info...":
                            {
                                string theModuleType = m.GetType().Name.ToString();
                                ModuleDescriptionDlg md = new ModuleDescriptionDlg( theModuleType );
                                md.Show();
                                break;
                            }

                        }
                    }
                }
            }
        }



#if NOT_USED
        private async void buttonReloadNetwork_click( object sender, RoutedEventArgs e )
        {
            var toSave = await MessageBox.YesNoCancel( "Would you like to SaveAs before reloading current file", "Reload File" );
            if( toSave == MessageBox.Buttons.Yes )
            {
                var filepath = await this.SaveAsDialog();
                if( filepath.Length == 0 )
                {
                    MessageBox.Alert( "No filename to SaveTo so just reloading", "Reload File" );
                }
                else
                {
                    var was = CurrentFileName;
                    if( this.SaveFile( CurrentFileName ) == false )
                    {
                        MessageBox.Alert( "Failed to Save current state to file: " + filepath + "carrying on with Reload", "Reload File" );
                    }
                    CurrentFileName = was;
                }
            }
            else if( toSave == MessageBox.Buttons.Cancel )
            {
                return;
            }

            if( CurrentFileName != "" )
            {
                if( this.LoadFile( CurrentFileName ) == false )
                {
                    MessageBox.Alert( "Failed to Load file:" + CurrentFileName, "Reload File" );
                    return;
                }
            }
        }
#endif

        private async void AvailableModules_SelectionChanged( object sender, SelectionChangedEventArgs e )
        {
            if( sender is AutoCompleteBox && e.AddedItems.Count > 0)
            {
                var autoComp = ( AutoCompleteBox )sender;
                if( autoComp.SelectedItem is not null )
                {
                    var moduleName = autoComp.SelectedItem as string;
                    autoComp.SelectedItem = null;
                    autoComp.Text = "";

                    ActivateModule( moduleName );
                }
            }
        }

        // General methods
        private bool CanSaveCurrentUKS()
        {
            // TODO see if the current USK is dirty?
            return true;
        }

        public ModuleBase CreateNewModule( string moduleTypeLabel, string moduleLabel = "" )
        {
            Type t = Type.GetType( "BrainSimulator.Modules." + moduleTypeLabel );
            if( t is null )
                return null;
            ModuleBase theModule = ( Modules.ModuleBase )Activator.CreateInstance( t );

            theModule.Label = moduleLabel;
            if( moduleLabel == "" )
                theModule.Label = moduleTypeLabel;

            theModule.GetUKS();
            return theModule;
        }

        private void SetupPythonEnv()
        {
#if PYTHON_SUPPORT
            //setup the python support
            pythonPath = ( string )Environment.GetEnvironmentVariable( "PythonPath", EnvironmentVariableTarget.User );
            if( string.IsNullOrEmpty( pythonPath ) )
            {
                var result1 = MessageBox.Show( "Do you want to use Python Modules?", "Python?", MessageBoxButton.YesNo );
                if( result1 == MessageBoxResult.Yes )
                {
                    string likeliPath = ( string )Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData );
                    likeliPath += @"\Programs\Python";
                    System.Windows.Forms.OpenFileDialog openFileDialog = new()
                    {
                        Title = "SELECT path to Python .dll (or cancel for no Python support)",
                        InitialDirectory = likeliPath,
                    };

                    // Show the file Dialog.  
                    System.Windows.Forms.DialogResult result = openFileDialog.ShowDialog();
                    // If the user clicked OK in the dialog and  
                    if( result == System.Windows.Forms.DialogResult.OK )
                    {
                        pythonPath = openFileDialog.FileName;
                        Environment.SetEnvironmentVariable( "PythonPath", pythonPath, EnvironmentVariableTarget.User );
                    }
                    else
                    {
                        Environment.SetEnvironmentVariable( "PythonPath", "", EnvironmentVariableTarget.User );
                    }
                    openFileDialog.Dispose();
                }
                else
                {
                    pythonPath = "no";
                    Environment.SetEnvironmentVariable( "PythonPath", pythonPath, EnvironmentVariableTarget.User );
                }
            }
            moduleHandler.PythonPath = pythonPath;
            if( pythonPath != "no" )
            {
                moduleHandler.InitPythonEngine();
            }
#endif
        }

        public void InitializeActiveModules()
        {
            for (int i = 0; i < activeModules.Count; i++)
            {
                ModuleBase mod = activeModules[i];
                if (mod is not null)
                {
                    mod.SetUpAfterLoad();
                }
            }
        }

        public void SetupBeforeSave()
        {
            for (int i = 0; i < activeModules.Count; i++)
            {
                ModuleBase mod = activeModules[i];
                if (mod is not null)
                {
                    mod.SetUpBeforeSave();
                }
            }
        }

        public void ShowAllModuleDialogs()
        {
            foreach (ModuleBase mb in activeModules)
            {
                if (mb is not null)
                {
                    Dispatcher.UIThread.Invoke((Action)delegate
                    {
                        mb.ShowDialog();
                    });
                }
            }
        }

        public void CreateEmptyUKS()
        {
            theUKS.AtomicThoughts.Clear();
            theUKS = new UKS.UKS();
            theUKS.CreateInitialStructure();  //creates the "thought" substructure

            if (theUKS.Labeled("BrainSim") is null)
                theUKS.AddThought("BrainSim", null);
            theUKS.GetOrAddThought("AvailableModule", "BrainSim");
            theUKS.GetOrAddThought("ActiveModule", "BrainSim");

            InsertMandatoryModules();
            InitializeActiveModules();
        }

        public void UpdateModuleListsInUKS()
        {
            theUKS.GetOrAddThought( "BrainSim", null );
            theUKS.GetOrAddThought( "AvailableModule", "BrainSim" );
            theUKS.GetOrAddThought( "ActiveModule", "BrainSim" );
            var availableListInUKS = theUKS.Labeled( "AvailableModule" ).Children;

            //add any missing modules
            var CSharpModules = Utils.GetListOfExistingCSharpModuleTypes();
            foreach( var module in CSharpModules )
            {
                string name = module.Name;
                Thought availableModule = availableListInUKS.FindFirst( x => x.Label == name );
                if( availableModule is null )
                    theUKS.GetOrAddThought( name, "AvailableModule" );
            }
            var PythonModules = moduleHandler.GetListOfExistingPythonModuleTypes();
            foreach( var name in PythonModules )
            {
                Thought availableModule = availableListInUKS.FindFirst( x => x.Label == name );
                if( availableModule is null )
                    theUKS.GetOrAddThought( name, "AvailableModule" );
            }
            //delete any non-existant modules
            availableListInUKS = theUKS.Labeled( "AvailableModule" ).Children;
            foreach( Thought t in availableListInUKS )
            {
                string name = t.Label;
                if( CSharpModules.FindFirst( x => x.Name == name ) is not null ) continue;
                if( PythonModules.FindFirst( x => x == name ) is not null ) continue;
                theUKS.DeleteAllChildren( t );
                t.Delete();
            }

            List<string> availableModules = new List<string>();

            //reconnect/delete any active modules
            var activeListInUKS = theUKS.Labeled( "ActiveModule" ).Children;
            foreach( Thought t in activeListInUKS )
            {
                var cleanName = t.Label.Substring( 0, t.Label.Length - 1 );
                Thought parent = availableListInUKS.FindFirst( x => x.Label == cleanName );
                if( parent is not null )
                {

                    Debug.WriteLine( "UpdateModuleListsInUKS() ActiveList: " + cleanName );
                    availableModules.Add( cleanName );
                    t.AddParent( parent );
                }
                else
                    t.Delete();
            }

            availableModules.Sort();
            
            // update the control
            AvailableModules.ItemsSource = availableModules;
        }

        public void InsertMandatoryModules()
        {
            // Debug.WriteLine("InsertMandatoryModules entered");
            ActivateModule("ModuleUKS");
            ActivateModule("ModuleUKSStatement");
        }

        public string ActivateModule(string moduleType)
        {
            Thought t = theUKS.GetOrAddThought(moduleType, "AvailableModule");
            t = theUKS.CreateInstanceOf(theUKS.Labeled(moduleType));
            t.AddParent(theUKS.Labeled("ActiveModule"));

            if (!moduleType.Contains(".py"))
            {
                ModuleBase newModule = CreateNewModule(moduleType);
                if (newModule is null) return "";
                newModule.Label = t.Label;
                activeModules.Add(newModule);
                newModule.OpenDlg();
            }
#if SUPPORT_PYTHON
            else
            {
                pythonModules.Add(t.Label);
            }
#endif

            ReloadActiveModulesSP();
            return t.Label;
        }

#if NOT_USED
        public ModuleBase GetModuleByLabel(string label)
        {
            return activeModules.FindFirst(x => x.Label == label);
        }
#endif

        private void DeleteModule( string moduleName )
        {
            ModuleBase mb = activeModules.FindFirst( x => x.Label == moduleName );
            mb.CloseDlg();
            mb.Closing();
            activeModules.Remove( mb );
            theUKS.Labeled( mb.Label )?.Delete();
            ReloadActiveModulesSP();
        }

        private void CloseAllModuleDialogs()
        {
            lock (activeModules)
            {
                foreach (ModuleBase md in activeModules)
                {
                    if (md is not null)
                    {
                        md.CloseDlg();
                    }
                }
            }
        }

        private void CloseAllModules()
        {
            lock (activeModules)
            {
                foreach (ModuleBase mb in activeModules)
                {
                    if (mb is not null)
                    {
                        mb.Closing();
                    }
                }
            }
#if PYTHON_SUPPORT
            foreach (string pythonModule in pythonModules)
            {
                moduleHandler.Close(pythonModule);
            }
            pythonModules.Clear();
#endif
        }

        public static void SuspendEngine()
        {
            timer.Stop();
        }

        public static void ResumeEngine()
        {
            timer.Start();
        }

        //THIS IS THE MAIN ENGINE LOOP
        private void Dt_Tick(object? sender, EventArgs e)
        {
            Thought activeModuleParent = theUKS.Labeled("ActiveModule");
            if (activeModuleParent is null) return;
            foreach (Thought module in activeModuleParent.Children)
            {
                ModuleBase mb = activeModules.FindFirst(x => x.Label == module.Label);
                if (mb is not null)
                {
                    Dispatcher.UIThread.Invoke((Action)delegate
                    {
                        mb.Fire();
                    });
                }
            }
#if PYTHON_SUPPORT
            foreach (string pythonModule in pythonModules)
            {
                moduleHandler.RunScript(pythonModule);
            }
#endif
        }

        private void NewFile()
        {
            SuspendEngine();

            CloseAllModuleDialogs();
            CloseAllModules();
            UnloadActiveModules();

            CreateEmptyUKS();
            CurrentFileName = "";

            UpdateModuleListsInUKS();

            LoadActiveModules();
            ReloadActiveModulesSP();
            ShowAllModuleDialogs();

            ResumeEngine();
        }

        private bool SaveFile( string fileName )
        {
            bool ret = false;

            SuspendEngine();
            SetupBeforeSave();

            if( theUKS.SaveUKStoXMLFile( fileName ) == true )
            {
                CurrentFileName = fileName;
                ret = true;
            }

            ResumeEngine();
            return ret;
        }

        private bool LoadFile( string fileName )
        {
            SuspendEngine();

            CloseAllModuleDialogs();
            CloseAllModules();

            UnloadActiveModules();

            if( !theUKS.LoadUKSfromXMLFile( fileName ) )
            {
                theUKS = new UKS.UKS();
                return false;
            }

            if( theUKS.Labeled( "BrainSim" ) is null )
            {
                MessageBox.Alert( "Labeled BrainSim Missing", "Failed to load" );
                return false;
            }
                
            this.CurrentFileName = fileName;

            UpdateModuleListsInUKS();

            LoadActiveModules();
            ReloadActiveModulesSP();
            ShowAllModuleDialogs();

            ResumeEngine();
            return true;
        }


        public void ReloadActiveModulesSP()
        {
            ActiveModuleSP.Children.Clear();

            Thought activeModuleParent = theUKS.Labeled( "ActiveModule" );
        
            if( activeModuleParent is null ) { return; }
            var activeModules1 = activeModuleParent.Children;
            activeModules1 = activeModules1.OrderBy( x => x.Label ).ToList();

            foreach( Thought t in activeModules1 )
            {
                //what kind of module is this?
                Thought t1 = t.Parents.FindFirst( x => x.HasAncestor( "AvailableModule" ) );
                if( t1 is null ) continue;
                string moduleType = t1.Label;

                TextBlock tb = new TextBlock();
                tb.Text = t.Label;
                tb.Margin = new Thickness( 5, 2, 5, 2 );
                tb.Padding = new Thickness( 10, 3, 10, 3 );
                tb.ContextMenu = new ContextMenu();
                if( moduleType.Contains( ".py" ) )
                { 
                }
                else
                {
                    // RHC Bug. Sometimes mod is null
                    ModuleBase? mod = activeModules.FindFirst( x => x.Label == t.Label );
                    if( mod is not null )
                        CreateContextMenu( mod, tb, tb.ContextMenu );
                }
                tb.Background = new SolidColorBrush( Colors.LightGreen );
                ActiveModuleSP.Children.Add( tb );
            }
        }
        void UnloadActiveModules()
        {
            Thought activeModulesParent = theUKS.Labeled( "ActiveModule" );
            if( activeModulesParent is null ) return;
            var activeModules1 = activeModulesParent.Children;

            foreach( Thought t in activeModules1 )
            {
                for( int i = 0; i < t.LinksTo.Count; i++ )
                {
                    Link r = t.LinksTo[ i ];
                    t.RemoveLink( r );
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

            var activeModules1 = theUKS.Labeled( "ActiveModule" ).Children;
            activeModules1 = activeModules1.OrderBy( x => x.Label ).ToList();

            foreach( Thought t in activeModules1 )
            {
                //what kind of module is this?
                Thought tModuleType = t.Parents.FindFirst( x => x.HasAncestor( "AvailableModule" ) );
                if( tModuleType is null ) continue;
                string moduleType = tModuleType.Label;

                if( moduleType.Contains( ".py" ) )
                {
#if PYTON_SUPPORT
                    pythonModules.Add(t.Label);
#endif
                }
                else
                {
                    ModuleBase mod = CreateNewModule( moduleType, t.Label );
                    if( mod is not null )
                        activeModules.Add( mod );
                    else
                    {
                        theUKS.Labeled( "ActiveModule" ).RemoveChild( t );
                    }
                }
            }
        }

        private void AddFileToMRUList( string filePath )
        {
            StringCollection MRUList = ( StringCollection )Properties.Settings.Default[ "MRUList" ];
            if( MRUList is null )
                MRUList = new StringCollection();
            MRUList.Remove( filePath ); //remove it if it's already there
            MRUList.Insert( 0, filePath ); //add it to the top of the list

            Properties.Settings.Default[ "MRUList" ] = MRUList;
            Properties.Settings.Default.Save();

            LoadMRUMenu();
        }

        public void RemoveFileFromMRUList( string filePath )
        {
            StringCollection MRUList = ( StringCollection )Properties.Settings.Default[ "MRUList" ];
            if( MRUList is null )
                MRUList = new StringCollection();
            MRUList.Remove( filePath ); //remove it if it's already there

            Properties.Settings.Default[ "MRUList" ] = MRUList;
            Properties.Settings.Default.Save();

            LoadMRUMenu();
        }

        private void LoadMRUMenu()
        {
            MRUListMenu.Items.Clear();
            StringCollection MRUList = (StringCollection)Properties.Settings.Default["MRUList"];
            if (MRUList is null)
                MRUList = new StringCollection();

            foreach (string fileItem in MRUList)
            {
                if (fileItem is null) continue;

                string shortName = Path.GetFileNameWithoutExtension(fileItem);
                MenuItem mi = new MenuItem() { Header = shortName };
                mi.Click += MainMenu_FileOpen;
                ToolTip.SetTip( mi, fileItem );
                MRUListMenu.Items.Add(mi);
            }
        }
               

        public void CreateContextMenu( ModuleBase nr, Control r, ContextMenu cm = null ) //for a selection
        {
            //cmCancelled = false;
            if( cm is null )
                cm = new ContextMenu();
            cm.SetValue( moduleNameProperty, nr.Label );

            StackPanel sp;
            MenuItem mi = new MenuItem();
            mi = new MenuItem();
            mi.Header = "Delete";
            mi.Click += ActiveModulesSP_ContextMenu_Click;
            cm.Items.Add( mi );

            mi = new MenuItem();
            mi.Header = "Initialize";
            mi.Click += ActiveModulesSP_ContextMenu_Click;
            cm.Items.Add( mi );

            if( supportCodeEditing == true )
            {
                mi = new MenuItem();
                mi.Header = "View Source";
                mi.Click += ActiveModulesSP_ContextMenu_Click;
                cm.Items.Add( mi );

                mi = new MenuItem();
                mi.Header = "View Dialog Source";
                mi.Click += ActiveModulesSP_ContextMenu_Click;
                cm.Items.Add( mi );
            }

            mi = new MenuItem();
            mi.Header = "Info...";
            mi.Click += ActiveModulesSP_ContextMenu_Click;
            cm.Items.Add( mi );

            mi = new MenuItem();

            ModuleBase m = activeModules.FindFirst( x => x.Label == nr.Label );
            int i = activeModules.IndexOf( m );

            if( activeModules[ i ] is not null )
            {
                var t = activeModules[ i ].GetType();
                Type t1 = Type.GetType( t.ToString() + "Dlg" );
                while( t1 is null && t.BaseType.Name != "ModuleBase" )
                {
                    t = t.BaseType;
                    t1 = Type.GetType( t.ToString() + "Dlg" );
                }
                if( t1 is not null )
                {
                    cm.Items.Add( new MenuItem { Header = "Show Dialog" } );
                    ( ( MenuItem )cm.Items[ cm.Items.Count - 1 ] ).Click += ActiveModulesSP_ContextMenu_Click;
                    cm.Items.Add( new MenuItem { Header = "Hide Dialog" } );
                    ( ( MenuItem )cm.Items[ cm.Items.Count - 1 ] ).Click += ActiveModulesSP_ContextMenu_Click;
                }
            }
        }

        public static void OpenSource( string fileName )
        {
            Process process = new Process();
            string taskFile = @"C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\devenv.exe";
            ProcessStartInfo startInfo = new ProcessStartInfo( taskFile, "/edit " + fileName );
            process.StartInfo = startInfo;
            process.Start();
        }
    }
}
