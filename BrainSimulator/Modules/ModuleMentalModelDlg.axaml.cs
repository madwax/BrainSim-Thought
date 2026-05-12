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
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UKS;
using static BrainSimulator.Modules.ModuleOnlineInfo;

namespace BrainSimulator.Modules;

public partial class ModuleMentalModelDlg : ModuleBaseDlg
{
    // Used to track the current pointer as we don't have access to this data like we did in WPF
    struct PointerCache
    {
        public PointerCache() 
        {
        }

        // Mouse position relative to the canvas
        public Point pos = new Point( 0,0 );
        public bool leftDown = false;
        public bool rightDown = false;
    };

    private Point _panStart;
    private bool _isPanning;
    private bool _suppressZoom;

    private PointerCache _pointer = new();

    // We have to cache the objects because they can't be named?
    private ScaleTransform? canvasScale = null;
    private TranslateTransform? canvasTranslate = null;

    public ModuleMentalModelDlg()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded( object? sender, RoutedEventArgs e )
    {
        ZoomSlider.Value = 1.0;
        var transformGroup = ( TransformGroup )theCanvas.RenderTransform;
        foreach( Transform t in transformGroup.Children )
        {
            if( t is ScaleTransform sform )
            {
                canvasScale = sform;
            }
            else if( t is TranslateTransform tform )
            {
                canvasTranslate = tform;
            }
        }
    }

    public override bool Draw( bool checkDrawTimer )
    {
        if( !base.Draw( checkDrawTimer ) ) return false;
        //this has a timer so that no matter how often you might call draw, the dialog
        //only updates 10x per second
        ModuleMentalModel parent = ( ModuleMentalModel )base.ParentModule;
        DrawCells( parent );
        return true;
    }
    private void DrawCells( ModuleMentalModel parent )
    {
        if( theCanvas is null ) return;
        if( theCanvas.IsPointerOver && this._pointer.rightDown == false ) // Mouse.RightButton != MouseButtonState.Pressed )
        {
            SetStatus( "Paused" );
            return;
        }
        if( GetStatus() == "Paused" )
            SetStatus( "OK" );
        theCanvas.Children.Clear();

        var cells = parent._cells;
        if( cells is null || cells.Length == 0 ) return;

        var sz = this.Bounds.Size;
        double canvasWidth = Math.Max( 1, sz.Width );
        double canvasHeight = Math.Max( 1, sz.Height );

        int ringCount = cells.Length;
        // Build elevation band edges from the binning function; fallback to uniform if mismatch
        List<double> edges = BuildElevationEdges( parent, ringCount );
        double yAcc = 0;

        for( int r = ringCount - 1; r >= 0; r-- )
        {
            if( cells[ r ] is null || cells[ r ].Length == 0 ) continue;

            double ringHeight = ( ( edges[ r + 1 ] - edges[ r ] ) / 180.0 ) * canvasHeight;
            int rays = cells[ r ].Length;
            //double cellWidth = canvasWidth / rays;
            double y = yAcc;
            yAcc += ringHeight;

            // Precompute warped x-edges for this ring
            double[] xEdges = new double[ rays + 1 ];
            for( int i = 0; i <= rays; i++ )
            {
                double t = ( double )i / rays;          // [0,1]
                double x = ( t * 2.0 ) - 1.0;           // [-1,1]
                double u = parent.InvertWarp( x ); // warped [-1,1]
                xEdges[ i ] = ( u + 1.0 ) * 0.5 * canvasWidth;          // [0,width]
            }

            for( int k = 0; k < rays; k++ )
            {
                double xLeft = xEdges[ k ];

                //double x = k * cellWidth;
                double cellWidth = Math.Max( 1e-3, xEdges[ k + 1 ] - xLeft );

                var rect = new Rectangle
                {
                    Width = cellWidth,
                    Height = ringHeight,
                    Fill = Brushes.DarkBlue,
                    Stroke = Brushes.DarkGray,
                    StrokeThickness = 1,
                };

                Thought t = cells[ r ][ k ];
                rect.Tag = t.Label;
                rect.PointerPressed += Rect_PointerLeftButtonDown;

                if( cells[ r ][ k ] == parent.Center )
                    rect.Fill = Brushes.Pink;
                if( t.LastFiredTime > DateTime.Now - TimeSpan.FromSeconds( 1 ) )
                    rect.Fill = Brushes.AliceBlue;
                Link ThoughtAtLocation = t.LinksTo.FindFirst( x => x.LinkType.Label == "_mm:contains" );
                if( ThoughtAtLocation is not null )
                {
                    rect.Fill = Brushes.Yellow;
                    if( ThoughtAtLocation.To.Label == "attention" ) rect.Fill = Brushes.Green;

                    var containsLinks = t.LinksTo.Where( x => x.LinkType.Label == "_mm:contains" ).ToList();

                    var toolTip = string.Join( "\r\n", containsLinks.Select( FormatContainsTooltip ) );

                    ToolTip.SetTip( rect, toolTip );
                    ToolTip.SetVerticalOffset( rect, 22 );
                    ToolTip.SetHorizontalOffset( rect, 22 );
                }
                Canvas.SetLeft( rect, xLeft );
                Canvas.SetTop( rect, y );
                theCanvas.Children.Add( rect );
            }
        }
    }

    private static List<double> BuildElevationEdges( ModuleMentalModel parent, int ringCount )
    {
        List<double> edges = new() { -90 };
        int lastBin = parent.GetBinFromElevation( -90 );

        for( double deg = -89.5; deg <= 90.0; deg += 0.5 )
        {
            int bin = parent.GetBinFromElevation( ( float )deg );
            if( bin != lastBin )
            {
                edges.Add( deg );
                lastBin = bin;
            }
        }
        edges.Add( 90 );

        // Fallback to uniform spacing if the binning did not produce the expected count
        if( edges.Count != ringCount + 1 )
        {
            edges = Enumerable.Range( 0, ringCount + 1 )
                              .Select( i => -90 + i * ( 180.0 / ringCount ) )
                              .ToList();
        }
        return edges;
    }

    private void Rect_PointerLeftButtonDown( object? sender, PointerPressedEventArgs e )
    {
        Debug.WriteLine( "*.Rect_PointerLeftButtonDown() Pos:" + this._pointer.pos + " left:" + this._pointer.leftDown + " right:" + this._pointer.rightDown + " pan:" + _isPanning );


        if( ParentModule is not ModuleMentalModel module ) return;
        if( sender is not Rectangle r ) return;
        if( e.Properties.IsLeftButtonPressed == false ) return;

        var labelToLookUp = r.Tag.ToString();

        Debug.WriteLine( "*.Rect_PointerLeftButtonDown() label:" + labelToLookUp );

        Thought t = module.theUKS.Labeled( labelToLookUp );
        if( t != null )
        {
            t.Fire();
            t.LinksTo.FindFirst( x => x.LinkType.Label == "above" )?.To.Fire();
            t.LinksTo.FindFirst( x => x.LinkType.Label == "rightOf" )?.To.Fire();
        }
    }

    private void TheGrid_SizeChanged( object sender, SizeChangedEventArgs e )
    {
        Draw( false );
    }

    private void ZoomSlider_ValueChanged( object sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e )
    {
        if( _suppressZoom ) return;

        var sz = this.Bounds.Size;

        Point focus = this._pointer.pos;

        if( double.IsNaN( this._pointer.pos.X ) || double.IsNaN( this._pointer.pos.Y ) || this._pointer.pos == default )
            focus = new Point( sz.Width * 0.5, sz.Height * 0.5 );

        Debug.WriteLine( "*.ZoomSlider_ValueChanged() value:" + ZoomSlider.Value + " Pos:" + focus );

        ApplyZoom( ZoomSlider.Value, focus );
    }

    private void ResetView_Click( object sender, RoutedEventArgs e )
    {
        _suppressZoom = true;
        ZoomSlider.Value = 1.0;
        _suppressZoom = false;

        canvasTranslate.X = 0;
        canvasTranslate.Y = 0;
        canvasScale.ScaleX = 1;
        canvasScale.ScaleY = 1;
    }

    private void RotateLeft_Click( object sender, RoutedEventArgs e )
    {
        if( ParentModule is not ModuleMentalModel module ) return;
        module.RotateMentalModel( Angle.FromDegrees( 15 ), Angle.FromDegrees( 0 ) );
        Draw( false );
    }

    private void RotateRight_Click( object sender, RoutedEventArgs e )
    {
        if( ParentModule is not ModuleMentalModel module ) return;
        module.RotateMentalModel( Angle.FromDegrees( -15 ), Angle.FromDegrees( 0 ) );
        Draw( false );
    }

    private void RotateUp_Click( object sender, RoutedEventArgs e )
    {
        if( ParentModule is not ModuleMentalModel module ) return;
        module.MoveMentalModel( 5f );
        Draw( false );
    }

    private void RotateDown_Click( object sender, RoutedEventArgs e )
    {
        if( ParentModule is not ModuleMentalModel module ) return;
        module.MoveMentalModel( -5f );
        Draw( false );
    }

    private void TheCanvas_MouseWheel( object sender, PointerWheelEventArgs e )
    {
        this._pointer.pos = e.GetPosition( theCanvas );
        this._pointer.rightDown = e.Properties.IsRightButtonPressed;
        this._pointer.leftDown = e.Properties.IsLeftButtonPressed;

        double delta = e.Delta.X > 0 ? 0.1 : -0.1;

        double target = Math.Clamp( ZoomSlider.Value + delta, ZoomSlider.Minimum, ZoomSlider.Maximum );


        Debug.WriteLine( "*.TheCanvas_MouseWheel() value:" + target + " Pos:" + this._pointer.pos );

        // use the last know mouse position relative to the canvas.
        ApplyZoom( target, this._pointer.pos );

        _suppressZoom = true;
        ZoomSlider.Value = target;
        _suppressZoom = false;
    }

    private void TheCanvas_MouseMove( object sender, PointerEventArgs e )
    {
        this._pointer.pos = e.GetPosition( theCanvas );
        this._pointer.rightDown = e.Properties.IsRightButtonPressed;
        this._pointer.leftDown = e.Properties.IsLeftButtonPressed;

        Debug.WriteLine( "*.TheCanvas_MouseMove() Pos:" + this._pointer.pos + " in pan:" + this._isPanning );

        if( !_isPanning ) return;

        Vector delta = this._pointer.pos - this._panStart;

        canvasTranslate.X += delta.X;
        canvasTranslate.Y += delta.Y;

        Debug.WriteLine( "*.TheCanvas_MouseMove()   Delta: " + delta );

        /*

        // use parent (untransformed) space to avoid twitch when the canvas moves
        var parent = theCanvas.Parent as Visual;
        if( parent is not null )
        {
            Point current = e.GetPosition( parent );
            Vector delta = current - _panStart;
            _panStart = current;

            canvasTranslate.X += delta.X;
            canvasTranslate.Y += delta.Y;
        }
        */
    }

    private void TheCanvas_PointerPressed( object? sender, PointerPressedEventArgs e )
    {
        this._pointer.pos = e.GetPosition( theCanvas );
        this._pointer.rightDown = e.Properties.IsRightButtonPressed;
        this._pointer.leftDown = e.Properties.IsLeftButtonPressed;

        Debug.WriteLine( "*.TheCanvas_MouseMove() Pos:" + this._pointer.pos + " in pan:" + this._isPanning );

        // only want left mouse button.
        if( this._pointer.rightDown == false )
        {
            e.Handled = false;
        }
        else
        {
            this._isPanning = true;
            this._panStart = this._pointer.pos;
        }
        Debug.WriteLine( "*.TheCanvas_PointerReleased() Pos:" + this._pointer.pos + " left:" + this._pointer.leftDown + " right:" + this._pointer.rightDown + " pan:" + _isPanning );
    }

    private void TheCanvas_PointerReleased( object? sender, PointerReleasedEventArgs e )
    {
        this._pointer.pos = e.GetPosition( theCanvas );
        this._pointer.rightDown = e.Properties.IsRightButtonPressed;
        this._pointer.leftDown = e.Properties.IsLeftButtonPressed;

        if( _isPanning == true && this._pointer.rightDown == false )
        {
            Debug.WriteLine( "   No Panning" );
            _isPanning = false;
        }
        else
        {
            e.Handled = false;
        }

        Debug.WriteLine( "*.TheCanvas_PointerReleased() Pos:" + this._pointer.pos + " left:" + this._pointer.leftDown + " right:" + this._pointer.rightDown + " pan:" + this._isPanning );
    }

    private void TheCanvas_MouseLeave( object sender, PointerEventArgs e )
    {
        /*
        this._pointer.pos = e.GetPosition( theCanvas );
        this._pointer.rightDown = e.Properties.IsRightButtonPressed;
        this._pointer.leftDown = e.Properties.IsLeftButtonPressed;

        //_isPanning = false;

        Debug.WriteLine( "*.TheCanvas_MouseLeave() Pos:" + this._pointer.pos + " left:" + this._pointer.leftDown + " right:" + this._pointer.rightDown + " pan:" + _isPanning );
        //theCanvas.ReleaseMouseCapture();
        */
    }

    private void TheCanvas_MouseEnter( object sender, PointerEventArgs e )
    {
        /*
        this._pointer.pos = e.GetPosition( theCanvas );
        this._pointer.rightDown = e.Properties.IsRightButtonPressed;
        this._pointer.leftDown = e.Properties.IsLeftButtonPressed;

        _isPanning = ( _isPanning == true && this._pointer.rightDown == true );

        Debug.WriteLine( "*.TheCanvas_MouseEnter() Pos:" + this._pointer.pos + " left:" + this._pointer.leftDown + " right:" + this._pointer.rightDown + " pan:" + _isPanning );
        //theCanvas.ReleaseMouseCapture();
        */
    }

    private void ApplyZoom( double newScale, Point focus )
    {
        double oldScale = canvasScale.ScaleX;
        newScale = Math.Clamp( newScale, ZoomSlider.Minimum, ZoomSlider.Maximum );
        if( Math.Abs( newScale - oldScale ) < 1e-6 ) return;

        double ratio = newScale / oldScale;

        // Adjust translate so the focus point stays under the cursor
        canvasTranslate.X = focus.X * ( 1 - ratio ) + canvasTranslate.X * ratio;
        canvasTranslate.Y = focus.Y * ( 1 - ratio ) + canvasTranslate.Y * ratio;

        canvasScale.ScaleX = newScale;
        canvasScale.ScaleY = newScale;
    }

    private string FormatContainsTooltip( Link l )
    {
        string label = l.To?.Label ?? "(null)";
        double d = GetDistanceFromLink( l );
        if( d > 0 )
            return $"{label} (d={d:0.})";
        return label;
    }

    private double GetDistanceFromLink( Link l )
    {
        var dLink = l.LinksTo.FirstOrDefault( x => x.LinkType?.Label == "distance" );
        if( dLink?.To?.Label?.StartsWith( "distance:" ) == true &&
            double.TryParse( dLink.To.Label[ "distance:".Length.. ], out double val ) )
            return val;
        return 0;
    }

}