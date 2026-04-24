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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Avalonia.Controls.Templates;

using UKS;
using Avalonia.LogicalTree;
using System.Configuration;


namespace BrainSimulator.Modules;

public partial class ModuleUKSDlg : ModuleBaseDlg
{
    /// <summary>
    /// Holds the number of labels and links so we only update
    /// the label when we have to.
    /// </summary>
    private struct RenderingInfo
    {
        public RenderingInfo() { }
        public int labels = 0;
        public int links = 0;
    };

    private struct TreeViewInfo
    {
        public TreeViewInfo() { }

        // Mouse pointer is over the treeview so don't do an update
        public bool pointerOver = false;
        // some ui element wants to stop Drawing()
        public bool contextMenuOpen = false;
        // When the context menu is closed do we force a draw?
        public bool contextMenuForceDraw = false;

        // This flag is used to rebuild expandedItemsLabels by enuming over all current treeview items and checking if they are expanded. 
        public bool rebuildExpendedItems = true;
        // The list of the labels of expanded treeviewitems.
        public List<string> expandedItemsLabels = new();
        // The currently selected treeviewItem label. 
        public string selectedLabel = "";
        // The label to expand on the next draw
        public string? expandLabel = null;
        public int totalItemCount = 0;
    };

    public static readonly StyledProperty<Thought> ThoughtObjectProperty = AvaloniaProperty.Register<TreeViewItem, Thought>( "Thought" );
    public static readonly StyledProperty<TreeViewItem> TreeViewItemProperty = AvaloniaProperty.Register<TreeViewItem, TreeViewItem>( "Thought" );
    public static readonly StyledProperty<Thought> LinkObjectProperty = AvaloniaProperty.Register<TreeViewItem, Thought>( "Thought" );

    private const int maxDepth = 20;
    private const int rootHistoryLimit = 8;
    private readonly List<string> _rootHistory = new();

    // runtime information used to control how Draw() is done. 
    // This holds flags light temp pauseing the timer updating the treeview while mouse is over it.
    private RenderingInfo renderCache = new();

    // runtime information used to update the treeview with UKS information
    // This holds flags and values used to rebuild the treeview
    private TreeViewInfo treeviewCache = new();

    //    private bool mouseInWindow; //prevent auto-update while the mouse is in the tree
    //    private bool pauseRefreshing = false;  // prevent the tree control being updated while we do things like context menus.
    private bool isTextChangingInternally = true;  //lockout so we can change the text without retriggering the event

    // Timer used to update the view of UKS if you are activily using the dialog
    private DispatcherTimer? updateViewTimer = new DispatcherTimer();

    private UKS.UKS theUKS = null;

    public ModuleUKSDlg()
    {
        InitializeComponent();
        theUKS = MainWindow.theUKS;
        this.updateViewTimer.Tick += OnUpdateViewTimer;
    }

    // Dialog Events
    //@{
    private void OnLoaded( object? sender, RoutedEventArgs e )
    {
        ModuleUKS parent = ( ModuleUKS )ParentModule;
        LoadRootHistory( parent.GetSavedDlgAttribute( "RootHistory" ) );
        CurrentRoot.Text = parent.GetSavedDlgAttribute( "Root" );
    }

    private void OnOpened( object? sender, EventArgs e )
    {
        if( AutoRefresh.IsChecked == true )
        {
            RefreshButton.IsEnabled = false;
        }
        else
        {
            RefreshButton.IsEnabled = true;
        }

        this.StartTimer();

        this.Title = "The Universal Knowledge Store (UKS)  --  File: " + Path.GetFileNameWithoutExtension( theUKS.FileName );
    }
    private void OnClosed( object? sender, EventArgs e )
    {
        this.StopTimer();
    }

    private void OnUpdateViewTimer( object? sender, EventArgs e )
    {
        if( IsVisible == true && !this.treeviewCache.pointerOver )
        {
            Draw( true );
        }
    }
    //@}

    // General Settings Events
    //@{

    private void OnAutoRefreshChanged( object? sender, RoutedEventArgs e )
    {
        if( sender is not null && sender is CheckBox )
        {
            CheckBox refresh = sender as CheckBox;
            if( refresh.IsChecked == true )
            {
                StartTimer();
                // For the tree control to rebuild on next draw.
                RefreshButton.IsEnabled = false;
            }
            else
            {
                StopTimer();
                RefreshButton.IsEnabled = true;
            }
        }
    }

    private void OnGeneralSettingChanged( object? sender, RoutedEventArgs e )
    {
        Draw( false );
    }

    private void OnRefreshButton( object? sender, RoutedEventArgs e )
    {
        Draw( false );
    }

    private void OnInitializeButton( object? sender, RoutedEventArgs e )
    {
        ModuleUKS parent = ( ModuleUKS )base.ParentModule;

        theUKS.CreateInitialStructure();
        parent.Initialize();

        CollapseAll();

        this.treeviewCache.expandLabel = parent.GetSavedDlgAttribute( "ExpandAll" );
        if( this.treeviewCache.expandLabel is null ) this.treeviewCache.expandLabel = "";
        string root = parent.GetSavedDlgAttribute( "Root" );
        if( string.IsNullOrEmpty( root ) )
            root = "Thought";
        CurrentRoot.Text = root;

        Draw( false );
    }


    private void OnCurrentRootInputChanged( object? sender, TextChangedEventArgs e )
    {
        var searchText = CurrentRoot.Text;
        if( string.IsNullOrEmpty( searchText ) )
            return;

        var suggestions = ThoughtLabels.LabelList.Keys
            .Where( key => key.StartsWith( searchText, StringComparison.OrdinalIgnoreCase ) )
            .OrderBy( key => key )
            .ToArray<string>();

        if( suggestions is null || suggestions.Length <= 1 )
            return;

        // stops update crashes
        if( suggestions.Contains( searchText ) )
            return;

        CurrentRoot.ItemsSource = suggestions;
        CurrentRoot.IsDropDownOpen = true;
    }

    private void OnCurrentRootKeyUp( object? sender, KeyEventArgs e )
    {
        if( e.Key == Key.Enter || e.Key == Key.Tab )
        {
            if( CurrentRoot.SelectedItem is not null )
            {
                var labelOfRootNode = CurrentRoot.SelectedItem.ToString();

                ModuleUKS parent = ( ModuleUKS )base.ParentModule;
                parent.SetSavedDlgAttribute( "Root", labelOfRootNode );

                this.AddRootToHistory( labelOfRootNode );
            }
            else if( CurrentRoot.Text == "" )
            {
            }
            else
            {
                return;
            }

            this.Draw( false );
        }
    }

    private void OnCurrentRootHistory( object? sender, RoutedEventArgs e )
    {
        // It's a context menu but we want left click so have to wire it up!
        CurrentRootHistory.Open();
    }

    private void OnCurrentRootHistoryMenuItem( object? sender, RoutedEventArgs e )
    {
        if( sender is null || sender is not MenuItem )
        {
            return;
        }

        var menuItemIs = sender as MenuItem;

        CurrentRoot.Text = menuItemIs.Header.ToString();

        Draw( false );
    }

    private async void OnImport( object? sender, RoutedEventArgs e )
    {
        SetStatus( "" );

        var path = await Utils.OpenFileDialog( this, Utils.TitleBrainSimImport, Utils.FilterTextFile );

        if( string.IsNullOrEmpty( path ) ) return;

        if( string.IsNullOrWhiteSpace( path ) )
        {
            SetStatus( "Choose a file first." );
            return;
        }
        if( !File.Exists( path ) )
        {
            SetStatus( "File not found." );
            return;
        }

        try
        {
            ModuleUKS parent = ( ModuleUKS )base.ParentModule;
            // Run ingest off the UI thread to keep the window responsive
            await Task.Run( () => theUKS.ImportTextFile( path ) );

            SetStatus( "Success" );
        }
        catch( Exception ex )
        {
            //Mouse.OverrideCursor = null;
            MessageBox.Alert( "Import failed.\n\n" + ex.Message, "UKS Import" );
        }
        finally
        {
            //Mouse.OverrideCursor = null;
        }
    }

    private async void OnExport( object? sender, RoutedEventArgs e )
    {
        var path = await Utils.SaveFileDialog( this, Utils.TitleBrainSimExport, Utils.FilterTextFile );
        if( string.IsNullOrEmpty( path ) ) return;

        if( string.IsNullOrWhiteSpace( path ) )
        {
            SetStatus( "Choose a file first." );
            return;
        }
        try
        {
            ModuleUKS parent = ( ModuleUKS )base.ParentModule;
            //get the root to save the contents of from the UKS dialog root
            string root = parent.GetSavedDlgAttribute( "Root" );
            await Task.Run( () => theUKS.ExportTextFile( root, path ) );
            SetStatus( "Success" );
        }
        catch( Exception ex )
        {
            // RHC - Need better alert 
            MessageBox.Alert( "Import failed.\n\n" + ex.Message, "UKS Export" );
        }
        finally
        {
        }
    }

    //@}

    // TreeView Events
    //@{
    private void OnTheTreeViewSized( object? sender, SizeChangedEventArgs e )
    {
        if( AutoRefresh.IsChecked == true )
        {
            Draw( false );
        }
    }

    private void OnTheTreeViewPointerEnter( object? sender, PointerEventArgs e )
    {
        this.treeviewCache.pointerOver = true;
    }
    private void OnTheTreeViewPointerLeave( object? sender, PointerEventArgs e )
    {
        this.treeviewCache.pointerOver = false;
    }

    //using the mouse-wheel while pressing ctrl key changes the font size
    private void OnTheTreeViewPointerWheel( object? sender, PointerWheelEventArgs e )
    {
        // RHC - My understanding is if we have a modifier its down.
        if( e.KeyModifiers.HasFlag( KeyModifiers.Control ) )
        {
            if( e.Delta.Y < 0.0 )
            {
                if( TheTreeView.FontSize > 2 )
                    TheTreeView.FontSize -= 1;
            }
            else if( e.Delta.Y > 0.0 )
            {
                TheTreeView.FontSize += 1;
            }

            ModuleUKS parent = ( ModuleUKS )ParentModule;
            parent.SetSavedDlgAttribute( "fontSize", TheTreeView.FontSize.ToString() );
        }
    }

    // Double clicking and expanded are two different events in Avalonia.
    private void OnTheTreeViewItemDoubleTapped( object? sender, TappedEventArgs e )
    {
        if( sender is null || sender is not TreeViewItem ) { return; }

        e.Handled = true;

        TreeViewItem tvi = sender as TreeViewItem;
        if( tvi.IsExpanded )
            return;

        // force the expanded event to be fired.
        tvi.IsExpanded = true;
    }

    //the treeview is populated only with expanded items or it would contain the entire UKS content
    //when an item is expanded, its content needs to be created into the treeview
    private void OnTheTreeViewItemExpanded( object? sender, RoutedEventArgs e )
    {
        if( sender is null || sender is not TreeViewItem ) { return; }

        e.Handled = true;

        TreeViewItem tvi = sender as TreeViewItem;

        var children = tvi.GetVisualChildren();
        if( children == null || children.Count() == 1 )
            PopulateEmptyTreeViewItemWithChildren( tvi );
    }
    //@}

    // TreeView Items Context Menu Events
    //@{
    private void OnTheTreeViewItemContextMenuOpened( object? sender, RoutedEventArgs e )
    {
        // Set the needed flags to manage Drawing of the treeview.
        this.treeviewCache.contextMenuOpen = true;
        this.treeviewCache.contextMenuForceDraw = false;

        //when the context menu opens, focus on the label and position text cursor to end
        if( sender is ContextMenu cm )
        {
            Control cc = Utils.FindByName( cm, "RenameBox" );
            if( cc is TextBox tb )
            {
                tb.Focus();
                tb.CaretIndex = tb.Text.Length;
            }
        }
    }

    private void OnTheTreeViewItemContextMenuItem( object? sender, RoutedEventArgs e )
    {
        if( sender is null || sender is not MenuItem )
        {
            return;
        }

        var mi = sender as MenuItem;
        ContextMenu m = mi.Parent as ContextMenu;
        //handle setting parent to root
        Thought tParent = ( Thought )mi.GetValue( ThoughtObjectProperty );
        if( tParent is not null )
        {
            CurrentRoot.Text = tParent.Label;
            this.treeviewCache.contextMenuForceDraw = true;
        }
        Thought t = ( Thought )m.GetValue( ThoughtObjectProperty );
        if( t is null )
        {
            Link r = m.GetValue( LinkObjectProperty ) as Link;
            ( r.From as Thought ).RemoveLink( r );
            this.treeviewCache.contextMenuForceDraw = true;
            return;
        }
        ModuleUKS parent = ( ModuleUKS )ParentModule;

        switch( mi.Header )
        {
            case "Expand All":
                this.treeviewCache.expandLabel = t.Label;
                parent.SetSavedDlgAttribute( "ExpandAll", this.treeviewCache.expandLabel );

                this.treeviewCache.expandedItemsLabels.Clear();
                this.treeviewCache.expandedItemsLabels.Add( "|Thougth|Object" );

                // don't let the next Draw() do the rebuild of expandedItemsLabels.
                this.treeviewCache.rebuildExpendedItems = false;
                break;
            case "Collapse All":
                this.treeviewCache.expandLabel = "";
                parent.SetSavedDlgAttribute( "ExpandAll", this.treeviewCache.expandLabel );

                this.treeviewCache.expandedItemsLabels.Clear();
                this.treeviewCache.expandedItemsLabels.Add( "|Thougth|Object" );

                // don't let the next Draw() do the rebuild of expandedItemsLabels.
                this.treeviewCache.rebuildExpendedItems = false;
                break;
            case "Fetch GPT Info":
                //the following is an async call so an immediate refresh is not useful
                //ModuleGPTInfo.GetChatGPTData(t.Label);
                break;
            case "Fire":
                t.Fire();

#if MODULE_ACTION
                var ActionModule = MainWindow.theWindow?.activeModules.OfType<ModuleAction>().FirstOrDefault();
                if (ActionModule is not null)
                    ActionModule.TakeActrion(t);
#endif
                break;
            case "Delete":
                theUKS.DeleteAllChildren( t );
                t.Delete();
                break;

            case "Delete Child":
                //figure out which item (and its parent) clicked us
                TreeViewItem tvi = m.GetValue( TreeViewItemProperty );

                Visual? parent1 = Avalonia.VisualTree.VisualExtensions.GetVisualParent( ( Visual )tvi );

                while( parent1 is not null && !( parent1 is TreeViewItem ) )
                    parent1 = Avalonia.VisualTree.VisualExtensions.GetVisualParent( parent1 );

                Thought parentThought = ( Thought )parent1.GetValue( ThoughtObjectProperty );

                //now delete the link
                if( parentThought is not null && t is not null )
                    parentThought.RemoveChild( t );

                break;

            case "Make Root":
                CurrentRoot.Text = t.Label;
                AddRootToHistory( t.Label );
                break;

            default:
                // this stops the contextMenuFroceDraw being set.
                return;
        }
        this.treeviewCache.contextMenuForceDraw = true;
    }

    private void OnTheTreeViewItemContextMenuRenameBoxKeyDown( object? sender, KeyEventArgs e )
    {
        if( sender is null || sender is not TextBox )
        {
            return;
        }

        var tb = sender as TextBox;
        MenuItem mi = tb.Parent as MenuItem;
        ContextMenu cm = mi.Parent as ContextMenu;
        Thought t = ( Thought )cm.GetValue( ThoughtObjectProperty );
        string testName = tb.Text + e.Key;
        Thought testThought = ThoughtLabels.GetThought( testName );

        if( testName != "" && testThought is not null && testThought != t )
        {
            tb.Background = new SolidColorBrush( Colors.Pink );
            return;
        }

        tb.Background = new SolidColorBrush( Colors.White );
        if( e.Key == Key.Enter )
        {
            t.Label = tb.Text;
            //clear any time-to-live on this new image
            var foundLink = t.LinksFrom.FindFirst( x => x.LinkType.Label == "is-a" );
            if( foundLink is not null ) foundLink.TimeToLive = TimeSpan.MaxValue;

            // In WPF this hides the control, not close it! cm.IsOpen = false;
            cm.IsVisible = false;
        }
        else if( e.Key == Key.Escape )
        {
            cm.IsVisible = false;
        }
    }

    private void OnTheTreeViewItemContextMenuClosed( object? sender, RoutedEventArgs e )
    {
        this.treeviewCache.contextMenuOpen = false;
        if( this.treeviewCache.contextMenuForceDraw == true )
        {
            /// for the draw
            this.treeviewCache.contextMenuForceDraw = false;
            Draw( true );
        }
    }
    //@}

    private void StartTimer()
    {
        // Don't start if the autorefresh is not ticked.
        if( AutoRefresh is null || AutoRefresh.IsChecked == false )
            return;

        updateViewTimer.Start();
    }

    private void StopTimer()
    {
        updateViewTimer.Stop();
    }

    private void UpdateTreeView()
    {
        // Don't update the tree if the context menu is open
        if( this.treeviewCache.contextMenuOpen == true )
            return;

        try
        {
            if( this.treeviewCache.rebuildExpendedItems == true )
            {
                // we are to get a list of the labels of the currently expanded items
                this.treeviewCache.expandedItemsLabels.Clear();
                this.treeviewCache.selectedLabel = "";
                FindExpandedItems( TheTreeView.Items, "" );
            }
            else
            {
                // Always set back to true
                this.treeviewCache.rebuildExpendedItems = true;
            }

            // Time to rebuild the treeview.
            TheTreeView.Items.Clear();
            LoadContentToTreeView();
        }
        catch
        {
            // If something bad has happened then next time force FindExpandedItems to be called.
            this.treeviewCache.rebuildExpendedItems = true;
        }
    }


    public override bool Draw( bool checkDrawTimer )
    {
        // RHC - Base has no timers anymore!
        //this has a timer so that no matter how often you might call draw, the dialog
        //only updates 10x per second
        if( !base.Draw( checkDrawTimer ) ) return false;

        this.UpdateTreeView();

        // See if we should update the basic UKS info in the UI
        if( !( this.renderCache.labels == ThoughtLabels.GetLabelCount() && this.renderCache.links == ThoughtLabels.GetLinksCount() ) )
        {
            this.UKSInformation.Content = ThoughtLabels.GetLabelCount() + " Thoughts  " + ThoughtLabels.GetLinksCount() + " Links.";
            this.renderCache.labels = ThoughtLabels.GetLabelCount();
            this.renderCache.links = ThoughtLabels.GetLinksCount();
        }
        return true;
    }

    private void LoadContentToTreeView()
    {
        //get the parameters
        ModuleUKS parent = ( ModuleUKS )ParentModule;

        // The label we should expand on the first Draw()
        this.treeviewCache.expandLabel = parent.GetSavedDlgAttribute( "ExpandAll" );
        string root = parent.GetSavedDlgAttribute( "Root" );

        //root = "BrainSim";
        Thought Root = theUKS.Labeled( root );

        if( Root is null ) Root = ( Thought )"Thought";

        string sizeString = parent.GetSavedDlgAttribute( "fontSize" );
        int.TryParse( sizeString, out int fontSize );

        if( fontSize != 0 )
            TheTreeView.FontSize = fontSize;

        //if the root is null, display the roots instead of the contetn
        if( !string.IsNullOrEmpty( root ) )
        {
            this.treeviewCache.totalItemCount = 0;

            TreeViewItem tvi = new() { Header = Root.ToString() };
            tvi.ContextMenu = GetContextMenu( Root, tvi );
            tvi.IsExpanded = true; //always expand the top-level item
            TheTreeView.Items.Add( tvi );

            tvi.SetValue( ThoughtObjectProperty, Root );

            AddLinks( Root, tvi, 1, "" );
            AddChildren( Root, tvi, 0, Root.Label );
        }
        else //search for unattached Thoughts
        {
            for( int i = 0; i < theUKS.AtomicThoughts.Count; i++ )
            {
                Thought t1 = theUKS.AtomicThoughts[ i ];
                if( t1.Parents.Count == 0 )
                {
                    TreeViewItem tvi = new() { Header = t1.Label };
                    tvi.ContextMenu = GetContextMenu( t1, tvi );
                    TheTreeView.Items.Add( tvi );
                }
            }
        }
    }
    private void AddChildren( Thought t, TreeViewItem tvi, int depth, string parentLabel )
    {
        if( this.treeviewCache.totalItemCount > 500 ) return;

        depth++;
        if( depth > maxDepth ) return;

        List<Link> theChildren = t.LinksFrom.Where( x => x.LinkType.Label.StartsWith( "is-a" ) && x.To is not null ).ToList();
        theChildren = theChildren.OrderBy( x => x.From.Label ).ToList();

        if( detailsCB.IsChecked == true )
            theChildren = theChildren.OrderByDescending( x => x.From.Weight ).ToList();

        foreach( Link l in theChildren )
        {
            //"l" is the link defining the is-a link so child is the from of it
            var child = l.From;
            TreeViewItem tviChild = GetTreeChildFormatted( parentLabel, child );
            tvi.Items.Add( tviChild );

            tviChild.ContextMenu = GetContextMenu( child, tviChild );

            if( l.LinksTo.Count > 0 )  //there is provenance on this is-a link
                AddLinks( l, tviChild, 1, parentLabel );

            int childCount = child.Children.Count;
            int linkCount = child.LinksTo.Count( x => x.LinkType.Label != "is-a" );
            int linkFromCount = child.LinksFrom.Count;

            if( tviChild.IsExpanded )
            {
                // load children and links
                AddLinks( child, tviChild, 1, parentLabel );
                AddChildren( child, tviChild, depth, parentLabel + "|" + child.Label );
            }
        }
    }
    private void AddLinks( Thought t, TreeViewItem tvi, int depth, string parentLabel )
    {
        if( t.LinksTo.Count == 0 && t is not Link lnk ) return;

        //build the entry for the tabel of expanded items
        string currentLabel = "|" + parentLabel + "|" + t.Label;
        if( theUKS.IsSequenceElement( t ) ) //this skips over the level of the sequence element itself and only shows the links
            currentLabel = "|" + parentLabel;

        currentLabel = currentLabel.Replace( "||", "|" ); //needed to make top level work

        //add each of the links as a "child" of the parent entry
        //display is-a links if deteails are requested
        var sortedLinks = t.LinksTo.OrderBy( x => x?.LinkType?.Label ).ToList();
        if( detailsCB.IsChecked == false )
            sortedLinks = t.LinksTo.Where( x => x.LinkType.Label != "is-a" ).OrderBy( x => x?.LinkType?.Label ).ToList();

        foreach( Link l in sortedLinks )
        {
            if( showConditionals.IsChecked != true )
                if( l.HasProperty( "isCondition" ) || l.HasProperty( "isResult" ) ) continue; //hide conditionals

            TreeViewItem tviLink = GetTreeChildFormatted( currentLabel, l );
            tviLink.ContextMenu = GetLinkContextMenu( l );
            tvi.Items.Add( tviLink );

            if( tviLink.IsExpanded && l.LinksTo.Count > 0 ) //get provenance, etc. on this link
                AddLinks( l, tviLink, depth, currentLabel );

            if( tviLink.IsExpanded && theUKS.IsSequenceElement( l?.To ) ) //expand sequence elements
                AddLinks( l.To, tviLink, depth, currentLabel + "|" + l.ToString() );
        }
        if( reverseCB.IsChecked == true )
            AddLinksFrom( t, tvi, currentLabel );
    }

    private void AddLinksFrom( Thought t, TreeViewItem tvi, string parentLabel )
    {
        if( t.LinksFrom.Count == 0 && t is not Link lnk ) return;

        //add the entry to the entry of expanded items
        parentLabel = "|" + parentLabel + "|" + t.ToString();
        parentLabel = parentLabel.Replace( "||", "|" ); //needed to make top level work

        //add each of the links as a "child" of the parent entry
        //display is-a links if deteails are requested
        var sortedLinks = t.LinksFrom.OrderBy( x => x?.LinkType?.Label ).ToList();
        foreach( Link r in sortedLinks )
        {
            if( showConditionals.IsChecked != true )
                if( r.HasProperty( "isCondition" ) || r.HasProperty( "isResult" ) ) continue; //hide conditionals

            TreeViewItem tviLink = GetTreeChildFormatted( parentLabel, r );
            tviLink.ContextMenu = GetLinkContextMenu( r );
            tvi.Items.Add( tviLink );
        }
    }

    //build and format the TreeView item for this Thought
    private TreeViewItem GetTreeChildFormatted( string parentLabel, Thought t )
    {
        Thought child = t;
        string header = "";
        if( t is SeqElement s1 )
        { }
        else if( t is Link r )
        {
            //format a link-like line in the treeview
            header = r.ToString();
            //show sequence content unless details are selected
            if( r.From is SeqElement )
            {
                if( r.LinkType.Label == "VLU" || r.LinkType.Label == "duration" )
                {
                    header = $"[{r.From.Label}→{r.LinkType.Label}→{r.To.Label}]";
                    if( r.To.Label == "" )
                        header = $"[{r.From.Label}→{r.LinkType.Label}→{r.To.ToString()}]";
                }
            }
            if( r.To is SeqElement s )
            {
                string joinCharacter = " ";
                if( r.LinkType.Label == "events" ) joinCharacter = "\n\t\t"; //hack for better dieplay of longer items
                if( r.LinkType.Label == "NXT" || r.LinkType.Label == "FRST" )
                {
                    header = $"[{r.From.Label}→{r.LinkType.Label}→{r.To.Label}]";
                }
                else
                {
                    var seqElementLabels = theUKS.FlattenSequence( s ).Select( x => x?.Label );
                    seqElementLabels = seqElementLabels
                        .Select( s =>
                        {
                            int i = s.IndexOf( ':' );
                            return i >= 0 ? s[ ( i + 1 ).. ] : s;
                        } ).ToList();
                    string sequence = "^" + string.Join( joinCharacter, seqElementLabels );
                    header = $"[{r.From.Label}→{r.LinkType.Label}→{sequence}]";
                }
            }
        }
        else
        {
            //Format a node-like line in the treeview
            header = child.ToString();
            if( header == "" )
                header = "\u25A1"; //put in a small empty box--if the header is unlabeled, so you can right-click 
        }

        header = AddDetails( t, header );

        //create the treeview entry
        TreeViewItem tviChild = new() { Header = header };

        //change color of thoughts which just fired or are about to expire
        tviChild.SetValue( ThoughtObjectProperty, child );
        if( child.LastFiredTime > DateTime.Now - TimeSpan.FromMilliseconds( 500 ) )
            tviChild.Background = new SolidColorBrush( Colors.LightGreen );

        if( child.TimeToLive != TimeSpan.MaxValue && child.LastFiredTime + child.TimeToLive < DateTime.Now + TimeSpan.FromSeconds( 3 ) )
            tviChild.Background = new SolidColorBrush( Colors.LightYellow );

        //is this expanded?
        string currentLabel = "|" + parentLabel + "|" + ( string.IsNullOrEmpty( child?.Label ) ? child?.ToString() : child?.Label );
        currentLabel = currentLabel.Replace( "||", "|" ); //parentLabel may or may not have a leading '|'

        if( this.treeviewCache.expandedItemsLabels.Contains( currentLabel ) )
            tviChild.IsExpanded = true;

        if( child.Ancestors.Contains( this.treeviewCache.expandLabel ) && ( child.Label == "" || !parentLabel.Contains( "|" + child.Label ) ) )
            tviChild.IsExpanded = true;

        if( this.treeviewCache.selectedLabel == currentLabel )
            tviChild.IsSelected = true;

        tviChild.Expanded += OnTheTreeViewItemExpanded;
        tviChild.DoubleTapped += OnTheTreeViewItemDoubleTapped;

        this.treeviewCache.totalItemCount++;
        return tviChild;
    }

    //if the "details" box is checked, add the details
    private string AddDetails( Thought t, string header )
    {
        if( detailsCB.IsChecked == false ) return header;
        string timeToLive = ( t.TimeToLive == TimeSpan.MaxValue ? "∞" : ( t.LastFiredTime + t.TimeToLive - DateTime.Now ).ToString( @"mm\:ss" ) );
        if( t.Weight != 1f || t.TimeToLive != TimeSpan.MaxValue )
            header = $"<{t.Weight.ToString( "f2" )}, {timeToLive}> " + header;
        if( t is Link r )
        {
            if( r.LinkType?.HasLink( null, null, theUKS.Labeled( "not" ) ) is not null ) //prepend ! for negative  children
                header = "!" + header;
        }
        header += ": Children:" + t.Children.Count;
        header += " Links:" + t.LinksTo.Count;
        return header;
    }

    private void PopulateEmptyTreeViewItemWithChildren( TreeViewItem tvi )
    {
        string name = tvi.Header.ToString(); // to help debug
        Thought t = ( Thought )tvi.GetValue( ThoughtObjectProperty );
        string parentLabel = "|" + t.ToString();
        TreeViewItem tvi1 = tvi;
        int depth = 0;
        //work your way up the tree to find all ancestors of this leaf
        while( tvi1.Parent is not null && tvi1.Parent is TreeViewItem tvi2 )
        {
            tvi1 = tvi2;
            Thought t1 = ( Thought )tvi1.GetValue( ThoughtObjectProperty );
            parentLabel = "|" + ( string.IsNullOrEmpty( t1?.Label ) ? t1?.ToString() : t1?.Label ) + parentLabel;
            depth++;
        }

        if( !this.treeviewCache.expandedItemsLabels.Contains( parentLabel ) )
            this.treeviewCache.expandedItemsLabels.Add( parentLabel );

        tvi.Items.Clear(); // delete empty child

        if( t.Children.Count > 0 )
            AddChildren( t, tvi, depth, parentLabel );

        if( t.LinksTo.Count > 0 )
            AddLinks( t, tvi, 1, parentLabel );

        if( reverseCB.IsChecked == true && t.LinksFrom.Count > 0 )
            AddLinksFrom( t, tvi, parentLabel );

        if( theUKS.IsSequenceElement( ( t as Link )?.To ) )
            AddLinks( ( t as Link )?.To, tvi, 1, parentLabel );

        tvi.IsExpanded = true;
        this.treeviewCache.selectedLabel = parentLabel;
    }

    //find out which tree items are already expanded by recursively following the tree items 
    private void FindExpandedItems( ItemCollection items, string parentLabel )
    {
        foreach( TreeViewItem tvi1 in items )
        {
            var t = tvi1.GetValue( ThoughtObjectProperty );
            if( t is null ) continue;

            var labelIs = parentLabel + "|" + t.ToString();
            if( tvi1.IsExpanded ) this.treeviewCache.expandedItemsLabels.Add( labelIs );
            if( tvi1.IsSelected ) this.treeviewCache.selectedLabel = labelIs;

            FindExpandedItems( tvi1.Items, labelIs );
        }
    }

    //Context Menu creation and handling
    private ContextMenu GetContextMenu( Thought t, TreeViewItem tvi )
    {
        ContextMenu menu = new ContextMenu()
        {
            Name = "ContextMenu"
        };
        menu.Classes.Add( "ContextMenu" );

        menu.SetValue( ThoughtObjectProperty, t );
        menu.SetValue( TreeViewItemProperty, tvi );
        int ID = theUKS.AtomicThoughts.IndexOf( t );
        MenuItem mi = new();
        string thoughtLabel = "___";
        if( t is not null )
            thoughtLabel = t.Label;
        mi.Header = "Name: " + thoughtLabel + "  Index: " + ID;
        mi.IsEnabled = false;
        menu.Items.Add( mi );

        TextBox renameBox = new() { Text = thoughtLabel, Width = 200, Name = "RenameBox" };
        renameBox.KeyDown += OnTheTreeViewItemContextMenuRenameBoxKeyDown;
        mi = new();
        mi.Header = renameBox;
        menu.Items.Add( mi );

        mi = new();
        mi.Click += OnTheTreeViewItemContextMenuItem;
        if( t.Label == this.treeviewCache.expandLabel )
            mi.Header = "Collapse All";
        else
            mi.Header = "Expand All";
        menu.Items.Add( mi );
        mi = new();
        mi.Click += OnTheTreeViewItemContextMenuItem;
        mi.Header = "Delete";
        menu.Items.Add( mi );

        mi = new();
        mi.Click += OnTheTreeViewItemContextMenuItem;
        mi.Header = "Delete Child";
        menu.Items.Add( mi );

        mi = new();
        mi.Click += OnTheTreeViewItemContextMenuItem;
        mi.Header = "Make Root";
        menu.Items.Add( mi );
        mi = new();
        mi.Click += OnTheTreeViewItemContextMenuItem;
        mi.Header = "Fire";
        menu.Items.Add( mi );

#if NOT_USED
        //mi = new();
        //mi.Click += OnTheTreeViewItemContextMenuItem;
        //mi.Header = "Fetch GPT Info";
        //menu.Items.Add(mi);
#endif

        mi = new();
        mi.Header = "Parents:";
        if( t.Parents.Count == 0 )
            mi.Header = "Parents: NONE";
        mi.IsEnabled = false;
        menu.Items.Add( mi );
        foreach( Thought t1 in t.Parents )
        {
            mi = new();
            mi.Click += OnTheTreeViewItemContextMenuItem;
            mi.Header = "    " + t1.Label;
            mi.SetValue( ThoughtObjectProperty, t1 );
            menu.Items.Add( mi );
        }

        menu.Opened += OnTheTreeViewItemContextMenuOpened;
        menu.Closed += OnTheTreeViewItemContextMenuClosed;

        return menu;
    }
    private ContextMenu GetLinkContextMenu( Link r )
    {
        ContextMenu menu = new()
        {
            Name = "ContextMenuLinks"
        };
        menu.Classes.Add( "ContextMenu" );

        menu.SetValue( LinkObjectProperty, r );
        MenuItem mi = new();
        mi.Header = $"Weight:  {r.Weight.ToString( "0.00" )}";
        mi.IsEnabled = false;
        menu.Items.Add( mi );
        mi = new();
        string timeToLive = ( r.TimeToLive == TimeSpan.MaxValue ? "∞" : ( r.LastFiredTime + r.TimeToLive - DateTime.Now ).ToString( @"mm\:ss" ) );
        mi.Header = $"TTL: {timeToLive}";
        mi.IsEnabled = false;
        menu.Items.Add( mi );
        mi = new();
        mi.Click += OnTheTreeViewItemContextMenuItem;
        mi.Header = "Delete";
        menu.Items.Add( mi );
        mi = new();
        mi.Header = "Go To:";
        mi.IsEnabled = false;
        menu.Items.Add( mi );

        mi = new();
        mi.Click += OnTheTreeViewItemContextMenuItem;
        mi.Header = "    " + r.From.Label;
        mi.SetValue( ThoughtObjectProperty, r.From );
        menu.Items.Add( mi );

        mi = new();
        mi.Click += OnTheTreeViewItemContextMenuItem;
        mi.Header = "    " + r.LinkType.Label;
        mi.SetValue( ThoughtObjectProperty, r.LinkType );
        menu.Items.Add( mi );

        mi = new();
        mi.Click += OnTheTreeViewItemContextMenuItem;
        mi.Header = "    " + r.To?.Label;
        mi.SetValue( ThoughtObjectProperty, r.To );
        menu.Items.Add( mi );

        return menu;
    }

    private void CollapseAll()
    {
        foreach( TreeViewItem item in TheTreeView.Items )
            CollapseTreeviewItems( item );
    }

    //recursively collapse all the children
    private void CollapseTreeviewItems( TreeViewItem Item )
    {
        Item.IsExpanded = false;

        foreach( TreeViewItem item in Item.Items )
        {
            item.IsExpanded = false;
            if( item.ItemCount > 0 )
                CollapseTreeviewItems( item );
        }
    }

    private void LoadRootHistory( string raw )
    {
        _rootHistory.Clear();
        if( !string.IsNullOrEmpty( raw ) )
            _rootHistory.AddRange( raw.Split( '|' ).Where( x => !string.IsNullOrWhiteSpace( x ) ) );
        UpdateRootHistoryItems();
    }

    private void AddRootToHistory( string value )
    {
        if( string.IsNullOrWhiteSpace( value ) ) return;

        var existing = _rootHistory.FindIndex( x => string.Equals( x, value, StringComparison.OrdinalIgnoreCase ) );

        if( existing >= 0 )
            _rootHistory.RemoveAt( existing );

        _rootHistory.Insert( 0, value );

        if( _rootHistory.Count > rootHistoryLimit )
            _rootHistory.RemoveRange( rootHistoryLimit, _rootHistory.Count - rootHistoryLimit );

        UpdateRootHistoryItems();

        if( ParentModule is ModuleUKS parent )
            parent.SetSavedDlgAttribute( "RootHistory", string.Join( "|", _rootHistory ) );
    }

    private void UpdateRootHistoryItems()
    {
        CurrentRootHistory.Items.Clear();

        foreach( var item in _rootHistory )
        {
            var menuItem = new MenuItem()
            {
                Header = item
            };
            menuItem.Click += this.OnCurrentRootHistoryMenuItem;
            CurrentRootHistory.Items.Add( menuItem );
        }
    }
}