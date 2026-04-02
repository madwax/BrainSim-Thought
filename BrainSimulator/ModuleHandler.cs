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
using System.Diagnostics;
using UKS;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;

// #define PYTHON_SUPPORT
// #define PYTHON_SUPPORT_UI

#if PYTHON_SUPPORT

#if !CONSOLE_APP
#define PYTHON_SUPPORT_UI
#endif

using Python.Runtime;

#if WINDOWS

using System.Windows.Interop;
using System.Windows;
using System.Runtime.InteropServices;

#elif MACOS

#else // Its Linux...

#endif

#endif



namespace BrainSimulator;

public class ModuleHandler
{
    public UKS.UKS theUKS = UKS.UKS.theUKS;

#if PYTHON_SUPPORT
    public List<string> pythonModules = new();

    public List<(string, dynamic)> activePythonModules = new();

    string pythonPath = "";
    public string PythonPath { get => pythonPath; set => pythonPath = value; }

    //Runtime.PythonDLL = @"/opt/anaconda3/envs/brainsim/bin/python";  // Yida's MAC
    //Runtime.PythonDLL = PythonDll;//  @"python310";  // Charles's Windows
#endif

    public string ActivateModule(string moduleType)
    {
        Thought t = theUKS.GetOrAddThought(moduleType, "AvailableModule");
        t = theUKS.CreateInstanceOf(theUKS.Labeled(moduleType));
        t.AddParent(theUKS.Labeled("ActiveModule"));

#if PYTHON_SUPPORT

#if PYTHON_SUPPORT_UI
        if( !moduleType.Contains(".py"))
        {
            BrainSimulator.Modules.ModuleBase newModule = MainWindow.theWindow.CreateNewModule(moduleType);
            newModule.Label = t.Label;
            MainWindow.theWindow.activeModules.Add(newModule);
            newModule.OpenDlg();
        }
        else
#endif
        {
            pythonModules.Add(t.Label);
        }
#endif
        return t.Label;
    }

    public void DeactivateModule(string moduleLabel)
    {
        Thought t = theUKS.Labeled(moduleLabel);
        if (t is null) return;
        for (int i = 0; i < t.LinksTo.Count; i++)
        {
            Link r = t.LinksTo[i];
            r.To.Delete();
        }
        t.Delete();

        return;
    }


    public List<string> GetListOfExistingPythonModuleTypes()
    {
#if PYTHON_SUPPORT
        //this is a buffer of python modules so they can be imported once and run many times.
        List<String> pythonFiles = new();
        if (pythonPath == "no") return pythonFiles;
        try
        {
            var filesInDir = Directory.GetFiles(@".", "m*.py").ToList();
            foreach (var file in filesInDir)
            {
                if (file.StartsWith("utils")) continue;
                if (file.Contains("template")) continue;
                pythonFiles.Add(Path.GetFileName(file));
            }
        }
        catch
        {

        }
        return pythonFiles;
#else
        return [];
#endif
    }

    public bool ClosePythonEngine()
    {
#if PYTHON_SUPPORT
        PythonEngine.Shutdown();
#endif
        return true;
    }
    public bool InitPythonEngine()
    {
#if PYTHON_SUPPORT
        try
        {
            //Runtime.PythonDLL = @"/opt/anaconda3/envs/brainsim/bin/python";  // Yida's MAC
            Runtime.PythonDLL = PythonPath;//  @"python310";  // Charles's Windows
            if (!PythonEngine.IsInitialized)
                PythonEngine.Initialize();
            dynamic sys = Py.Import("sys");
            dynamic os = Py.Import("os");
            string desiredPath = os.path.join(os.getcwd(), "./bin/Debug/net8.0/");
            sys.path.append(desiredPath);  // enables finding scriptName module
            sys.path.append(os.getcwd() + "\\pythonModules");
            Console.WriteLine("PythonEngine init succeeded\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Python engine initialization failed because: " + ex.Message);
            return false;
        }
#endif
        return true;
    }

    public void Close(string moduleLabel)
    {
#if PYTHON_SUPPORT
        var theModuleEntry = activePythonModules.FirstOrDefault(x => x.Item1.ToLower() == moduleLabel.ToLower());
        if (theModuleEntry.Item2 is not null)
        {
            if (theModuleEntry.Item2 is not null)
            {
                try
                {
                    theModuleEntry.Item2.Close();
                }
                catch { }
            }
        }
#endif
    }

    public void RunScript(string moduleLabel)
    {
#if PYTHON_SUPPORT
        if (PythonPath == "no") return;
        bool firstTime = false;
        //get the ModuleType
        Thought tModule = theUKS.Labeled(moduleLabel);
        if (tModule is null) { return; }
        Thought tModuleType = tModule.Parents.FindFirst(x => x.HasAncestor("AvailableModule"));
        if (tModuleType is null) return;
        string moduleType = tModuleType.Label;
        moduleType = moduleType.Replace(".py", "");

        //if this is the very first call, initialize the python engine
        if (Runtime.PythonDLL is null)
        {
            try
            {
                Runtime.PythonDLL = PythonPath;//  @"python310";  // Charles's Windows
                PythonEngine.Initialize();
                dynamic sys = Py.Import("sys");
                dynamic os = Py.Import("os");
                string desiredPath = os.path.join(os.getcwd(), "./bin/Debug/net8.0/");
                sys.path.append(desiredPath);  // enables finding scriptName module
                sys.path.append(os.getcwd() + "\\pythonModules");
                Console.WriteLine("PythonEngine init succeeded\n");
            }
            catch
            {
                Console.WriteLine("Python engine initialization failed");
                return;
            }
        }
        using (Py.GIL())
        {
            var theModuleEntry = activePythonModules.FirstOrDefault(x => x.Item1.ToLower() == moduleLabel.ToLower());
            if (string.IsNullOrEmpty(theModuleEntry.Item1))
            {
                //if this is the first time this modulw has been used
                try
                {
                    Console.WriteLine("Loading " + moduleLabel);
                    dynamic theModule = Py.Import(moduleType);
                    theModule.Init();
                    theModuleEntry = (moduleLabel, theModule);
                    activePythonModules.Add(theModuleEntry);
                    firstTime = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Load/initialize failed for module: " + moduleLabel + "   Reason: " + ex.Message);
                    theModuleEntry = (moduleLabel, null);
                    activePythonModules.Add(theModuleEntry);
                }
            }

            if (theModuleEntry.Item2 is not null)
            {
                try
                {
                    theModuleEntry.Item2.Fire();
#if PYTHON_SUPPORT_UI
#if WINDOWS
                    //This sets the owner of any target window so that the system will work propertly
                    if (firstTime)
                    {
                        var HWND = theModuleEntry.Item2.GetHWND();
                        var ss = HWND.ToString();
                        // this works, and returns 1322173
                        int intValue = Convert.ToInt32(ss, 16);
                        firstTime = false;
                        SetOwner(intValue);
                    }
#elif MACOS
    // TODO - Do we need to support parenting the phyton window?
#else
    // TODO - Do we need to support parenting the phyton window?
    // Also what about if we are running under X or Wayland?
#endif
#endif
                }
                catch (Exception ex)
                {
                    activePythonModules.Remove(theModuleEntry);
                    DeactivateModule(moduleLabel);
#if PYTHON_SUPPORT_UI
                    MainWindow.theWindow.ReloadActiveModulesSP();
#endif
                    Console.WriteLine("Fire method call failed for module: " + moduleLabel + "   Reason: " + ex.Message);
                }
            }
        }
#endif
    }

    public void ClearAllPythonModules()
    {
#if PYTHON_SUPPORT
        pythonModules.Clear();
        activePythonModules.Clear();
#endif        
    }


    public void CreateEmptyUKS()
    {
        theUKS = new UKS.UKS();
        if (theUKS.Labeled("BrainSim") is null)
            theUKS.AddThought("BrainSim", null);
        theUKS.GetOrAddThought("AvailableModule", "BrainSim");
        theUKS.GetOrAddThought("ActiveModule", "BrainSim");

        InsertMandatoryModules();
    }

    public void InsertMandatoryModules()
    {

        Debug.WriteLine("InsertMandatoryModules entered");
#if PYTHON_SUPPORT
#if PYTHON_SUPPORT_UI
        ActivateModule("UKS");
        ActivateModule("UKSStatement");

#endif
#else
        ActivateModule( "UKS" );
        ActivateModule( "UKSStatement" );
#endif
    }

#if PYTHON_SUPPORT
#if PYTHON_SUPPORT_UI

#if WINDOWS
    private const int GWL_HWNDPARENT = -8;

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    static void SetOwner(int HWND)
    {
        // Example usage
        IntPtr childHwnd = new IntPtr(HWND); // Child window handle
                                             //IntPtr ownerHwnd = new IntPtr(654321); // New owner window handle
        Window window = Window.GetWindow(MainWindow.theWindow);
        var wih = new WindowInteropHelper(window);
        IntPtr ownerHwnd = wih.Handle;

        ChangeWindowOwner(childHwnd, ownerHwnd);
    }

    static void ChangeWindowOwner(IntPtr childHwnd, IntPtr ownerHwnd)
    {
        IntPtr result = 0;
        if (IntPtr.Size == 8)
            SetWindowLongPtr(childHwnd, GWL_HWNDPARENT, ownerHwnd);
        else
            SetWindowLong(childHwnd, GWL_HWNDPARENT, ownerHwnd);

        if (result == IntPtr.Zero)
        {
            int errorCode = Marshal.GetLastWin32Error();
            Console.WriteLine($"Failed to change window owner. Error code: {errorCode}");
        }
        else
        {
            Console.WriteLine("Window owner changed successfully.");
        }
    }
#elif MACOS
#else // LINUX
#endif

#endif
#endif
}
