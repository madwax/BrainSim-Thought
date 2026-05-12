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
using Avalonia.Platform;
using AvaloniaGraphControl;
using UKS;

// TODO RHC Test Test Test
namespace BrainSimulator.Modules
{
    public partial class ModuleShowGraphDlg : ModuleBaseDlg
    {
        public ModuleShowGraphDlg()
        {
            InitializeComponent();
        }

        public override bool Draw(bool checkDrawTimer)
        {
            if (!base.Draw(checkDrawTimer)) return false;
            //this has a timer so that no matter how often you might call draw, the dialog
            //only updates 10x per second
            ModuleShowGraph parent = (ModuleShowGraph)base.ParentModule;
            //DrawTheGraph();
            return true;
        }

        private void TheGrid_SizeChanged(object? sender, SizeChangedEventArgs e)
        {
            //DrawTheGraph();
        }

        private void DrawTheGraph()
        {

            string root = "Object";
            ModuleShowGraph parent = ( ModuleShowGraph )base.ParentModule;
            Thought uksDlg = parent.theUKS.Labeled( "ModuleUKS0" );
            if( uksDlg is not null )
            {
                foreach( var r in uksDlg.LinksTo )
                {
                    if( r.LinkType.Label == "hasAttribute" && r.To.Label.StartsWith( "Root" ) )
                    {
                        root = ( string )r.To.V;
                    }
                }
            }

            /// TODO - RHC Needs more work!

            Graph graph = new();

            Thought theRoot = parent.theUKS.Labeled( root );
            foreach( Thought t in theRoot.Descendants )
            {
                var sourceNode = t.Label;
                foreach( Link r in t.LinksTo )
                {
                    if( r.From != theRoot )
                    {
                        var targetNode = r.To.Label;
                        var edgeLabel = r.LinkType.Label;
                        graph.Edges.Add( new Edge( sourceNode, targetNode, edgeLabel ) );
                    }
                }
            }

            this.theGraph.Graph = graph;
 
            /*
            //get the root to save the contents of from the UKS dialog root
            string root = "Object";
            ModuleShowGraph parent = (ModuleShowGraph)base.ParentModule;
            Thought uksDlg = parent.theUKS.Labeled("ModuleUKS0");
            if (uksDlg is not null)
                foreach (var r in uksDlg.LinksTo)
                {
                    if (r.LinkType.Label == "hasAttribute" && r.To.Label.StartsWith("Root"))
                    {
                        root = (string)r.To.V;
                    }
                }

            var viewer = new GViewer();

            var g = new Graph();

            g.Attr.BackgroundColor = Microsoft.Msagl.Drawing.Color.Black;
            viewer.OutsideAreaBrush = Brushes.Black;

            Thought theRoot = parent.theUKS.Labeled(root);
            foreach (Thought t in theRoot.Descendants)
            {
                foreach (Link r in t.LinksTo)
                {
                    if (r.From == theRoot) continue;
                    string label = r.LinkType.Label;
                    //foreach (Clause c in r.Clauses)
                    //    label += $"\n{c.clauseType.Label} {c.clause.Source.Label} {c.clause.LinkType.Label} {c.clause.Target.Label}";
                    var e = g.AddEdge(t.Label, label, r.To.Label);
                    e.Attr.Color = Microsoft.Msagl.Drawing.Color.Yellow;
                    e.Label.FontColor = Microsoft.Msagl.Drawing.Color.White;
                    e.Label.FontSize = 6;
                    e.SourceNode.Attr.Color = Microsoft.Msagl.Drawing.Color.Pink;
                    e.SourceNode.Attr.FillColor = Microsoft.Msagl.Drawing.Color.LightBlue;
                    //e.SourceNode.Attr.Shape = Microsoft.Msagl.Drawing.Shape.Circle; //doesn't really work


                    e.TargetNode.Attr.Color = Microsoft.Msagl.Drawing.Color.Pink;
                    e.TargetNode.Attr.FillColor = Microsoft.Msagl.Drawing.Color.LightBlue;
                    //e.TargetNode.Attr.Shape = Microsoft.Msagl.Drawing.Shape.Circle;
                }
            }

            viewer.Graph = g;
            wfHost.Child = viewer;   //
            */
        }

        private void ButtonRefresh_Click(object? sender, RoutedEventArgs e)
        {
            DrawTheGraph();
        }
    }
}

