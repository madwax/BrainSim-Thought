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
using Avalonia.Controls.Shapes;
using System;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO;

namespace BrainSimulator
{

    /// <summary>
    /// Class used to support development of Brain Sim.
    /// </summary>
    public class SourceEditing
    {
#if WINDOWS
        private static string[] VisualStudioFilepaths = [
            @"C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\devenv.exe",
            @"C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\IDE\devenv.exe",
            @"C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\devenv.exe",
            @"C:\Program Files\Microsoft Visual Studio\18\Professional\Common7\IDE\devenv.exe"
        ];

        private string visualStudioFilepath;
#endif
        private bool supported = false;
        private string sourceCodePath = "";

        public enum FileTypes
        {
            ModuleSource = 0,
            DialogLayout,
            DialogSource
        };

        private static string FindSourcePathFrom( string[] checkPaths )
        {
            foreach( var checkPath in checkPaths )
            {
                var binIndex = checkPath.LastIndexOf( System.IO.Path.DirectorySeparatorChar + "bin" );
                if( binIndex < 0 )
                {
                    // bin was not found so...
                    return "";
                }
                var sourceTreePath = checkPath.Substring( 0, binIndex );
                if( File.Exists( System.IO.Path.Combine( sourceTreePath, "App.axaml" ) ) == true )
                {
                    return sourceTreePath;
                }
            }
            return "";
        }

#if WINDOWS
        private static string FindVisualStudioPath()
        {
            foreach( var filepath in VisualStudioFilepaths )
            {
                if( System.IO.Path.Exists( filepath ) )
                {
                    return filepath;
                }
            }
            return "";
        }
#endif        

        public SourceEditing()
        {
            supported = false;

#if WINDOWS
            visualStudioFilepath = FindVisualStudioPath();
            if( visualStudioFilepath.Length > 0 )
            {
                supported = true;
            }
#endif
            if( supported == false )
                return;

            // find the root  of the BrianSim thought source code. 
            sourceCodePath = FindSourcePathFrom( [ Environment.ProcessPath, System.IO.Directory.GetCurrentDirectory() ] );
            if( sourceCodePath.Length == 0 )
            {
                // Disable 
                supported = false;
            }
        }

        public bool IsSupported()
        {
            return supported;
        }

        private string ExpectedSourceFilename( string moduleName, FileTypes mode )
        {
            if( mode == FileTypes.ModuleSource )
            {
                return System.IO.Path.Combine( sourceCodePath, "Modules", moduleName + ".cs" );
            }
            else if( mode == FileTypes.DialogLayout )
            {
                return System.IO.Path.Combine( sourceCodePath, "Modules", moduleName + "Dlg.axaml" );
            }
            else if( mode == FileTypes.DialogSource )
            {
                return System.IO.Path.Combine( sourceCodePath, "Modules", moduleName + "Dlg.axaml.cs" );
            }

            // should never but...
            throw new ArgumentException( "Calling SourceEditing.ExpectedSourceFilename with unknown mode: " + mode.ToString() );
        }


        /// <summary>
        /// Opens the modules sources or layout in your dev env.
        /// </summary>
        /// <param name="moduleName">The module you want to edit</param>
        /// <param name="mode">Which source file</param>
        public void OpenInEditor( string moduleName, FileTypes mode )
        {
            // silently return as we should never have gotten here.
            if( supported == false )
                return;

            // workout the filename of the source code file.
            string sourceFilepath = ExpectedSourceFilename( moduleName, mode );

#if WINDOWS
            Process process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo( visualStudioFilepath, "/edit " + sourceFilepath );
            process.StartInfo = startInfo;
            process.Start();
#endif
        }

        /// <summary>
        /// Opens all modules source code files in your editor.
        /// </summary>
        /// <param name="moduleName"></param>
        public void OpenAllSourcesInEditor( string moduleName )
        {
            OpenInEditor( moduleName, FileTypes.ModuleSource );
            OpenInEditor( moduleName, FileTypes.DialogSource );
            OpenInEditor( moduleName, FileTypes.DialogLayout );
        }
    }
}
