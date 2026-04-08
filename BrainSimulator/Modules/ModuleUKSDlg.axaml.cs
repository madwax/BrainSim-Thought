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


namespace BrainSimulator.Modules;

public partial class ModuleUKSDlg : ModuleBaseDlg
{
    private enum TreeViewExpandedModes
    {
        Currently = 0, // Workout what is currently expanded in the treeview.
        All, // Your doing to expand everything
        None // Only the root is expended.
    };

    public static readonly StyledProperty<Thought> ThoughtObjectProperty = AvaloniaProperty.Register<TreeViewItem, Thought>( "Thought" );
    public static readonly StyledProperty<TreeViewItem> TreeViewItemProperty = AvaloniaProperty.Register<TreeViewItem, TreeViewItem>( "Thought" );
    public static readonly StyledProperty<Thought> LinkObjectProperty = AvaloniaProperty.Register<TreeViewItem, Thought>( "Thought" );

    private const int maxDepth = 20;
    private const int rootHistoryLimit = 8;
    private readonly List<string> _rootHistory = new();
    private int totalItemCount;
    private bool mouseInWindow; //prevent auto-update while the mouse is in the tree
    private bool pauseRefreshing = false;  // prevent the tree control being updated while we do things like context menus.
    private bool contextMenuForceDraw = false; // True if the content menu needs to draw after its action.
    private List<string> treeviewCurrentExpandedItems = new();
    private string treeviewCurrentSelectedLabel = "";
    private TreeViewExpandedModes treeviewExpandedMode = TreeViewExpandedModes.Currently;

    private bool updateFailed;
    private DispatcherTimer? dt = null;
    private string expandAll = "";  //all the children below this named node will be expanded
    private UKS.UKS theUKS = null;


    public ModuleUKSDlg()
    {
        InitializeComponent();
        theUKS = MainWindow.theUKS;
        this.Opened += OnOpened;
        this.Closed += OnClosed;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        this.StartTimer();
    }
    private void OnClosed( object? sender, EventArgs e )
    {
        this.StopTimer();
    }

    private void StartTimer()
    {
        if( dt is null )
        {
            dt = new DispatcherTimer();
            dt.Tick += Dt_Tick;
        }

        if( checkBoxAuto.IsChecked == true )
        {
            dt.Start();
        }
    }
    private void StopTimer()
    {
        if( dt is null ) return;
        dt.Stop();
    }

    public override bool Draw(bool checkDrawTimer)
    {
        // RHC - Base has no timers anymore!

        //this has a timer so that no matter how often you might call draw, the dialog
        //only updates 10x per second
        if( !base.Draw(checkDrawTimer)) return false;

        if( pauseRefreshing == false )
        {
            Refresh();
        }
        return true;
    }

    private void LoadContentToTreeView()
    {
        //get the parameters
        ModuleUKS parent = (ModuleUKS)ParentModule;

        expandAll = parent.GetSavedDlgAttribute("ExpandAll");
        string root = parent.GetSavedDlgAttribute("Root");

        //root = "BrainSim";
        Thought Root = theUKS.Labeled(root);

        if (Root is null) Root = (Thought)"Thought";

        string sizeString = parent.GetSavedDlgAttribute("fontSize");
        int.TryParse(sizeString, out int fontSize);

        if (fontSize != 0)
            theTreeView.FontSize = fontSize;

        //if the root is null, display the roots instead of the contetn
        if (!string.IsNullOrEmpty(root))
        {
            totalItemCount = 0;

            TreeViewItem tvi = new() { Header = Root.ToString() };
            tvi.ContextMenu = GetContextMenu(Root, tvi);
            tvi.IsExpanded = true; //always expand the top-level item
            theTreeView.Items.Add(tvi);
            
            tvi.SetValue(ThoughtObjectProperty, Root);

            AddLinks(Root, tvi, 1, "");
            AddChildren(Root, tvi, 0, Root.Label);
        }
        else //search for unattached Thoughts
        {
            for ( int i = 0; i < theUKS.AtomicThoughts.Count; i++)
            {
                Thought t1 = theUKS.AtomicThoughts[i];
                if (t1.Parents.Count == 0)
                {
                    TreeViewItem tvi = new() { Header = t1.Label };
                    tvi.ContextMenu = GetContextMenu(t1, tvi);
                    theTreeView.Items.Add(tvi);
                }
            }
        }
    }
    private void AddChildren(Thought t, TreeViewItem tvi, int depth, string parentLabel)
    {
        if( totalItemCount > 500) return;
        depth++;
        if (depth > maxDepth) return;

        List<Link> theChildren = t.LinksFrom.Where(x => x.LinkType.Label.StartsWith("is-a") && x.To is not null).ToList();
        theChildren = theChildren.OrderBy(x => x.From.Label).ToList();

        if (detailsCB.IsChecked == true)
            theChildren = theChildren.OrderByDescending(x => x.From.Weight).ToList();

        foreach (Link l in theChildren)
        {
            //"l" is the link defining the is-a link so child is the from of it
            var child = l.From;
            TreeViewItem tviChild = GetTreeChildFormatted(parentLabel, child);
            tvi.Items.Add(tviChild);

            tviChild.ContextMenu = GetContextMenu(child, tviChild);

            if (l.LinksTo.Count > 0)  //there is provenance on this is-a link
                AddLinks(l, tviChild, 1, parentLabel);

            int childCount = child.Children.Count;
            int linkCount = child.LinksTo.Count(x => x.LinkType.Label != "is-a");
            int linkFromCount = child.LinksFrom.Count;

            if (tviChild.IsExpanded)
            {
                // load children and links
                AddLinks(child, tviChild, 1, parentLabel);
                AddChildren(child, tviChild, depth, parentLabel + "|" + child.Label);
            }
        }
    }
    private void AddLinks(Thought t, TreeViewItem tvi, int depth, string parentLabel)
    {
        if( t.LinksTo.Count == 0 && t is not Link lnk) return;

        //build the entry for the tabel of expanded items
        string currentLabel = "|" + parentLabel + "|" + t.Label;
        if (theUKS.IsSequenceElement(t)) //this skips over the level of the sequence element itself and only shows the links
            currentLabel = "|" + parentLabel;
        currentLabel = currentLabel.Replace("||", "|"); //needed to make top level work

        //add each of the links as a "child" of the parent entry
        //display is-a links if deteails are requested
        var sortedLinks = t.LinksTo.OrderBy(x => x?.LinkType?.Label).ToList();
        if (detailsCB.IsChecked == false)
            sortedLinks = t.LinksTo.Where(x => x.LinkType.Label != "is-a").OrderBy(x => x?.LinkType?.Label).ToList();
        foreach (Link l in sortedLinks)
        {
            if (showConditionals.IsChecked != true)
                if (l.HasProperty("isCondition") || l.HasProperty("isResult")) continue; //hide conditionals
            var x = treeviewCurrentExpandedItems;

            TreeViewItem tviLink = GetTreeChildFormatted(currentLabel, l);
            tviLink.ContextMenu = GetLinkContextMenu(l);
            tvi.Items.Add(tviLink);

            if (tviLink.IsExpanded && l.LinksTo.Count > 0) //get provenance, etc. on this link
                AddLinks(l, tviLink, depth, currentLabel);

            if (tviLink.IsExpanded && theUKS.IsSequenceElement(l?.To)) //expand sequence elements
                AddLinks(l.To, tviLink, depth, currentLabel + "|" + l.ToString());
        }
        if (reverseCB.IsChecked == true)
            AddLinksFrom(t, tvi, currentLabel);
    }
    private void AddLinksFrom(Thought t, TreeViewItem tvi, string parentLabel)
    {
        if (t.LinksFrom.Count == 0 && t is not Link lnk) return;

        //add the entry to the entry of expanded items
        parentLabel = "|" + parentLabel + "|" + t.ToString();
        parentLabel = parentLabel.Replace("||", "|"); //needed to make top level work

        //add each of the links as a "child" of the parent entry
        //display is-a links if deteails are requested
        var sortedLinks = t.LinksFrom.OrderBy(x => x?.LinkType?.Label).ToList();
        foreach (Link r in sortedLinks)
        {
            if (showConditionals.IsChecked != true)
                if (r.HasProperty("isCondition") || r.HasProperty("isResult")) continue; //hide conditionals

            TreeViewItem tviLink = GetTreeChildFormatted(parentLabel, r);
            tviLink.ContextMenu = GetLinkContextMenu(r);
            tvi.Items.Add(tviLink);
        }
    }

    //build and format the TreeView item for this Thought
    private TreeViewItem GetTreeChildFormatted(string parentLabel, Thought t)
    {
        Thought child = t;
        string header = "";
        if (t is SeqElement s1)
        { }
        else if (t is Link r)
        {
            //format a link-like line in the treeview
            header = r.ToString();
            //show sequence content unless details are selected
            if (r.From is SeqElement)
            {
                if (r.LinkType.Label == "VLU" || r.LinkType.Label == "duration")
                {
                    header = $"[{r.From.Label}→{r.LinkType.Label}→{r.To.Label}]";
                    if (r.To.Label == "")
                        header = $"[{r.From.Label}→{r.LinkType.Label}→{r.To.ToString()}]";
                }
            }
            if (r.To is SeqElement s)
            {
                string joinCharacter = " ";
                if (r.LinkType.Label == "events") joinCharacter = "\n\t\t"; //hack for better dieplay of longer items
                if (r.LinkType.Label == "NXT" || r.LinkType.Label == "FRST")
                {
                    header = $"[{r.From.Label}→{r.LinkType.Label}→{r.To.Label}]";
                }
                else
                {
                    var seqElementLabels = theUKS.FlattenSequence(s).Select(x => x?.Label);
                    seqElementLabels = seqElementLabels
                        .Select(s =>
                        {
                            int i = s.IndexOf(':');
                            return i >= 0 ? s[(i + 1)..] : s;
                        }).ToList();
                    string sequence = "^" + string.Join(joinCharacter, seqElementLabels);
                    header = $"[{r.From.Label}→{r.LinkType.Label}→{sequence}]";
                }
            }
        }
        else
        {
            //Format a node-like line in the treeview
            header = child.ToString();
            if (header == "")
                header = "\u25A1"; //put in a small empty box--if the header is unlabeled, so you can right-click 
        }

        header = AddDetails(t, header);

        //create the treeview entry
        TreeViewItem tviChild = new() { Header = header };

        //change color of thoughts which just fired or are about to expire
        tviChild.SetValue(ThoughtObjectProperty, child);
        if (child.LastFiredTime > DateTime.Now - TimeSpan.FromMilliseconds(500))
            tviChild.Background = new SolidColorBrush(Colors.LightGreen);

        if (child.TimeToLive != TimeSpan.MaxValue && child.LastFiredTime + child.TimeToLive < DateTime.Now + TimeSpan.FromSeconds(3))
            tviChild.Background = new SolidColorBrush(Colors.LightYellow);

        //is this expanded?
        string currentLabel = "|" + parentLabel + "|" + (string.IsNullOrEmpty(child?.Label) ? child?.ToString() : child?.Label);
        currentLabel = currentLabel.Replace("||", "|"); //parentLabel may or may not have a leading '|'

        switch( treeviewExpandedMode )
        {
            case TreeViewExpandedModes.Currently:
            {
                if( treeviewCurrentExpandedItems.Contains( currentLabel ) )
                    tviChild.IsExpanded = true;
            }break;

            case TreeViewExpandedModes.All:
            {
                tviChild.IsExpanded = true;
            } break;

            case TreeViewExpandedModes.None:
            {
                tviChild.IsExpanded = false;
            } break;
        }

        if( treeviewCurrentSelectedLabel == currentLabel )
            tviChild.IsSelected = true;

        if (child.Ancestors.Contains(expandAll) && (child.Label == "" || !parentLabel.Contains("|" + child.Label)))
            tviChild.IsExpanded = true;
        
        tviChild.Expanded += TreeViewItem_Expanded;
        tviChild.DoubleTapped += TreeViewItem_DoubleTapped;

        totalItemCount++;
        return tviChild;
    }

    //if the "details" box is checked, add the details
    private string AddDetails(Thought t, string header)
    {
        if (detailsCB.IsChecked == false) return header;
        string timeToLive = (t.TimeToLive == TimeSpan.MaxValue ? "∞" : (t.LastFiredTime + t.TimeToLive - DateTime.Now).ToString(@"mm\:ss"));
        if (t.Weight != 1f || t.TimeToLive != TimeSpan.MaxValue)
            header = $"<{t.Weight.ToString("f2")}, {timeToLive}> " + header;
        if (t is Link r)
        {
            if (r.LinkType?.HasLink(null, null, theUKS.Labeled("not")) is not null) //prepend ! for negative  children
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

        if( !treeviewCurrentExpandedItems.Contains( parentLabel ) )
            treeviewCurrentExpandedItems.Add( parentLabel );

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
        treeviewCurrentSelectedLabel = parentLabel;
    }

    private void TreeViewItem_DoubleTapped( object? sender, TappedEventArgs e )
    {
        Debug.WriteLine( "TreeViewItem_DoubleTapped() >>" );

        if( sender is null ) { return; }
        if( sender is not TreeViewItem ) { return; }
        TreeViewItem tvi = sender as TreeViewItem;

        e.Handled = true;

        var children = tvi.GetVisualChildren();
        if( children == null || children.Count() <= 1 )
        {
            PopulateEmptyTreeViewItemWithChildren( tvi );
        }
        Debug.WriteLine( "TreeViewItem_DoubleTapped() <<" );
    }

    //the treeview is populated only with expanded items or it would contain the entire UKS content
    //when an item is expanded, its content needs to be created into the treeview
    private void TreeViewItem_Expanded( object sender, RoutedEventArgs e)
    {
        Debug.WriteLine( "TreeViewItem_Expanded() >>" );

        if( sender is null ) { return; }
        if( sender is not TreeViewItem ) { return; }

        e.Handled = true;
        TreeViewItem tvi = sender as TreeViewItem;

        if( tvi.IsExpanded ) 
        {
            return; 
        }

        PopulateEmptyTreeViewItemWithChildren( tvi );
        var children = tvi.GetVisualChildren();
        if( children == null || children.Count() == 0 )
        {
            PopulateEmptyTreeViewItemWithChildren( tvi );
        }
        Debug.WriteLine( "TreeViewItem_Expanded() <<" );
    }

    //find out which tree items are already expanded by recursively following the tree items 
    private void FindExpandedItems(ItemCollection items, string parentLabel)
    {
        foreach (TreeViewItem tvi1 in items)
        {
            var t = tvi1.GetValue(ThoughtObjectProperty);
            if (t is null) continue;

            var lableIs = parentLabel + "|" + t.ToString();
            if (tvi1.IsExpanded) treeviewCurrentExpandedItems.Add(lableIs);
            if( tvi1.IsSelected ) treeviewCurrentSelectedLabel = lableIs;

            FindExpandedItems(tvi1.Items, lableIs);
        }
    }

    //Context Menu creation and handling
    private ContextMenu GetContextMenu(Thought t, TreeViewItem tvi)
    {
        ContextMenu menu = new ContextMenu();
        menu.SetValue(ThoughtObjectProperty, t);
        menu.SetValue(TreeViewItemProperty, tvi);
        int ID = theUKS.AtomicThoughts.IndexOf(t);
        MenuItem mi = new();
        string thoughtLabel = "___";
        if (t is not null)
            thoughtLabel = t.Label;
        mi.Header = "Name: " + thoughtLabel + "  Index: " + ID;
        mi.IsEnabled = false;
        menu.Items.Add(mi);

        TextBox renameBox = new() { Text = thoughtLabel, Width = 200, Name = "RenameBox" };
        renameBox.KeyDown += RenameBox_KeyDown;
        mi = new();
        mi.Header = renameBox;
        menu.Items.Add(mi);

        mi = new();
        mi.Click += Mi_Click;
        if (t.Label == expandAll)
            mi.Header = "Collapse All";
        else
            mi.Header = "Expand All";
        menu.Items.Add(mi);
        mi = new();
        mi.Click += Mi_Click;
        mi.Header = "Delete";
        menu.Items.Add(mi);

        mi = new();
        mi.Click += Mi_Click;
        mi.Header = "Delete Child";
        menu.Items.Add(mi);

        mi = new();
        mi.Click += Mi_Click;
        mi.Header = "Make Root";
        menu.Items.Add(mi);
        mi = new();
        mi.Click += Mi_Click;
        mi.Header = "Fire";
        menu.Items.Add(mi);

#if NOT_USED
        //mi = new();
        //mi.Click += Mi_Click;
        //mi.Header = "Fetch GPT Info";
        //menu.Items.Add(mi);
#endif

        mi = new();
        mi.Header = "Parents:";
        if (t.Parents.Count == 0)
            mi.Header = "Parents: NONE";
        mi.IsEnabled = false;
        menu.Items.Add(mi);
        foreach (Thought t1 in t.Parents)
        {
            mi = new();
            mi.Click += Mi_Click;
            mi.Header = "    " + t1.Label;
            mi.SetValue(ThoughtObjectProperty, t1);
            menu.Items.Add(mi);
        }

        menu.Opened += Menu_Opened;
        menu.Closed += Menu_Closed;
        ApplyContextMenuTheme(menu);   // honor OS theme
        return menu;
    }

    private void Menu_Closed(object sender, RoutedEventArgs e)
    {
        pauseRefreshing = false;
        if(contextMenuForceDraw == true)
        {
            Draw( true );
            contextMenuForceDraw = false;
        }
    }

    private void Menu_Opened(object sender, RoutedEventArgs e)
    {
        pauseRefreshing = true;

        //when the context menu opens, focus on the label and position text cursor to end
        if (sender is ContextMenu cm)
        {
            Control cc = Utils.FindByName(cm, "RenameBox");
            if (cc is TextBox tb)
            {
                tb.Focus();

                tb.CaretIndex = tb.Text.Length;

                // TODO - RHC Don't know if we should use this or what we have below
                tb.SelectionStart = 0;
                tb.SelectionEnd = tb.Text.Length;

            }
        }
    }

    private void RenameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox tb)
        {
            MenuItem mi = tb.Parent as MenuItem;
            ContextMenu cm = mi.Parent as ContextMenu;
            Thought t = (Thought)cm.GetValue(ThoughtObjectProperty);
            string testName = tb.Text + e.Key;
            Thought testThought = ThoughtLabels.GetThought(testName);
            if (testName != "" && testThought is not null && testThought != t)
            {
                tb.Background = new SolidColorBrush(Colors.Pink);
                return;
            }
            tb.Background = new SolidColorBrush(Colors.White);
            if (e.Key == Key.Enter)
            {
                t.Label = tb.Text;
                //clear any time-to-live on this new image
                var foundLink = t.LinksFrom.FindFirst(x => x.LinkType.Label == "is-a");
                if( foundLink is not null ) foundLink.TimeToLive = TimeSpan.MaxValue;

                // In WPF this hides the control, not close it! cm.IsOpen = false;
                cm.IsVisible = false;
            }
            else if( e.Key == Key.Escape )
            {
                cm.IsVisible = false;
            }
        }
    }

    private ContextMenu GetLinkContextMenu(Link r)
    {
        ContextMenu menu = new ContextMenu();
        menu.SetValue(LinkObjectProperty, r);
        MenuItem mi = new();
        mi.Header = $"Weight:  {r.Weight.ToString("0.00")}";
        mi.IsEnabled = false;
        menu.Items.Add(mi);
        mi = new();
        string timeToLive = (r.TimeToLive == TimeSpan.MaxValue ? "∞" : (r.LastFiredTime + r.TimeToLive - DateTime.Now).ToString(@"mm\:ss"));
        mi.Header = $"TTL: {timeToLive}";
        mi.IsEnabled = false;
        menu.Items.Add(mi);
        mi = new();
        mi.Click += Mi_Click;
        mi.Header = "Delete";
        menu.Items.Add(mi);
        mi = new();
        mi.Header = "Go To:";
        mi.IsEnabled = false;
        menu.Items.Add(mi);

        mi = new();
        mi.Click += Mi_Click;
        mi.Header = "    " + r.From.Label;
        mi.SetValue(ThoughtObjectProperty, r.From);
        menu.Items.Add(mi);

        mi = new();
        mi.Click += Mi_Click;
        mi.Header = "    " + r.LinkType.Label;
        mi.SetValue(ThoughtObjectProperty, r.LinkType);
        menu.Items.Add(mi);

        mi = new();
        mi.Click += Mi_Click;
        mi.Header = "    " + r.To?.Label;
        mi.SetValue(ThoughtObjectProperty, r.To);
        menu.Items.Add(mi);

        ApplyContextMenuTheme(menu);   // honor OS theme
        return menu;
    }

    private void Mi_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi)
        {
            ContextMenu m = mi.Parent as ContextMenu;
            //handle setting parent to root
            Thought tParent = (Thought)mi.GetValue(ThoughtObjectProperty);
            if (tParent is not null)
            {
                comboRoot.Text = tParent.Label;
                contextMenuForceDraw = true;
            }
            Thought t = (Thought)m.GetValue(ThoughtObjectProperty);
            if (t is null)
            {
                Link r = m.GetValue(LinkObjectProperty) as Link;
                (r.From as Thought).RemoveLink(r);
                contextMenuForceDraw = true;
                return;
            }
            ModuleUKS parent = (ModuleUKS)ParentModule;

            switch (mi.Header)
            {
                case "Expand All":
                    expandAll = t.Label;
                    treeviewCurrentExpandedItems.Clear();
                    treeviewCurrentExpandedItems.Add("|Thought|Object");
                    parent.SetSavedDlgAttribute("ExpandAll", expandAll);
                    treeviewExpandedMode = TreeViewExpandedModes.All;
                    break;
                case "Collapse All":
                    expandAll = "";
                    treeviewCurrentExpandedItems.Clear();
                    treeviewCurrentExpandedItems.Add("|Thought|Object");
                    treeviewExpandedMode = TreeViewExpandedModes.None;
                    parent.SetSavedDlgAttribute("ExpandAll", expandAll);
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
                    theUKS.DeleteAllChildren(t);
                    t.Delete();
                    break;

                case "Delete Child":
                    //figure out which item (and its parent) clicked us
                    TreeViewItem tvi = m.GetValue(TreeViewItemProperty);

                    Visual? parent1 = Avalonia.VisualTree.VisualExtensions.GetVisualParent((Visual)tvi);
                    
                    while (parent1 is not null && !(parent1 is TreeViewItem))
                        parent1 = Avalonia.VisualTree.VisualExtensions.GetVisualParent(parent1);
                    
                    Thought parentThought = (Thought)parent1.GetValue(ThoughtObjectProperty);
                    
                    //now delete the link
                    if (parentThought is not null && t is not null)
                        parentThought.RemoveChild(t);
                    
                    break;

                case "Make Root":
                    comboRoot.Text = t.Label;
                    AddRootToHistory(t.Label);
                    break;
            }

            //force a repaint
            contextMenuForceDraw = true;
        }
    }

    //EVENTS
    private void TheTreeView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        Draw(false);
    }

    private void UpdateStatusLabel()
    {
        statusLabel.Content = ThoughtLabels.GetLabelCount() + " Thoughts  " + ThoughtLabels.GetLinksCount() + " Links.";
        Title = "The Universal Knowledgs Store (UKS)  --  File: " + Path.GetFileNameWithoutExtension(theUKS.FileName);
    }

    private bool _isTextChangingInternally = true;  //lockout so we can change the text without retriggering the event

    private void TextBoxRoot_KeyDown(object sender, KeyEventArgs e)
    {
        TextBox? tb = comboRoot.FindNameScope().Find<TextBox>( "PART_EditableTextBox" );

        if (tb is null) return;
        // Allow text changes when keys like backspace, delete are pressed
        if (e.Key == Key.Back || e.Key == Key.Delete)
        {
            _isTextChangingInternally = true;
            int caretIndex = tb.CaretIndex;
            if (e.Key == Key.Back) caretIndex--;
            if (caretIndex < 0) caretIndex = 0;
            comboRoot.Text = comboRoot.Text.Substring(0, caretIndex);
            tb.CaretIndex = caretIndex;
            e.Handled = true;
            _isTextChangingInternally = false;
            if (e.Key == Key.Back)
                textBoxRoot_TextChanged(null, null);
        }
        if (e.Key == Key.Enter)
        {
            AddRootToHistory(comboRoot.Text);
            tb.ClearSelection();
        }
    }
    private void textBoxRoot_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isTextChangingInternally)
            return;

        string searchText = comboRoot.Text;
        if (!string.IsNullOrEmpty(searchText))
        {
            var suggestion = ThoughtLabels.LabelList.Keys
                .Where(key => key.StartsWith(searchText, StringComparison.OrdinalIgnoreCase))
                .OrderBy(key => key)
                .FirstOrDefault();
            if (suggestion is not null) suggestion = ThoughtLabels.GetThought(suggestion).Label;

            if (suggestion is not null && !suggestion.Equals(searchText, StringComparison.OrdinalIgnoreCase))
            {
                TextBox? tb = comboRoot.FindNameScope().Find<TextBox>( "PART_EditableTextBox" );

                if (tb is null) return;
                int caretIndex = tb.CaretIndex;
                _isTextChangingInternally = true;
                comboRoot.Text = suggestion;
                tb.CaretIndex = caretIndex;
                tb.SelectionStart = caretIndex;
                tb.SelectionEnd = suggestion.Length - caretIndex;
                // RHC - Do we have this?
                //tb.SelectionOpacity = .4;
                _isTextChangingInternally = false;
            }
        }
        ModuleUKS parent = (ModuleUKS)ParentModule;
        if (parent is null) return;
        parent.SetSavedDlgAttribute("Root", comboRoot.Text); //why?

        //Refresh();
        Draw( false );
    }
    //using the mouse-wheel while pressing ctrl key changes the font size
    private void theTreeView_MouseWheel(object sender, PointerWheelEventArgs e )
    {
        // RHC - My understanding is if we have a modifier its down.
        if( e.KeyModifiers.HasFlag( KeyModifiers.Control ) )
        {
            if( e.Delta.Y < 0.0 )
            {
                if( theTreeView.FontSize > 2 )
                    theTreeView.FontSize -= 1;

            }
            else if( e.Delta.Y > 0.0 )
            {
                theTreeView.FontSize += 1;
            }

            ModuleUKS parent = ( ModuleUKS )ParentModule;
            parent.SetSavedDlgAttribute( "fontSize", theTreeView.FontSize.ToString() );
        }
    }

    private void Dt_Tick(object sender, EventArgs e)
    {
        if( IsVisible == true )
        {
            if( !mouseInWindow )
                Draw( true );
            if( RefreshButton is not null ) RefreshButton.IsVisible = false;
        }
    }

    private void CheckBoxAuto_Checked( object sender, RoutedEventArgs e )
    {
        StartTimer();
        RefreshButton.IsVisible = false;
    }

    private void CheckBoxAuto_Unchecked(object sender, RoutedEventArgs e)
    {
        StopTimer();
        RefreshButton.IsVisible = true;
    }

    private void CheckBox_Checked(object sender, RoutedEventArgs e)
    {
        Draw(false);
    }

    private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        Draw(false);
    }

    private void TheTreeView_MouseEnter(object sender, PointerEventArgs e )
    {
        mouseInWindow = true;
        theTreeView.Background = new SolidColorBrush(Colors.LightSteelBlue);
    }
    private void TheTreeView_MouseLeave(object sender, PointerEventArgs e )
    {
        mouseInWindow = false;
        theTreeView.Background = new SolidColorBrush(Colors.LightGray);
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        //Refresh();
        Draw( false );
    }

    // Draw calls this which does the work.
    // if you call this then everything is getting updated and there are not checks stopping it.
    private void Refresh()
    {
        try
        {
            if (treeviewExpandedMode == TreeViewExpandedModes.Currently )
            {
                treeviewCurrentExpandedItems.Clear();
                treeviewCurrentSelectedLabel = "";
                FindExpandedItems(theTreeView.Items, "");
            }
            theTreeView.Items.Clear();
            LoadContentToTreeView();

            UpdateStatusLabel();
        }
        catch
        {
        }
    }

    private void InitializeButton_Click(object sender, RoutedEventArgs e)
    {
        ModuleUKS parent = (ModuleUKS)base.ParentModule;

        theUKS.CreateInitialStructure();
        parent.Initialize();

        CollapseAll();
        expandAll = parent.GetSavedDlgAttribute("ExpandAll");
        if (expandAll is null) expandAll = "";
        string root = parent.GetSavedDlgAttribute("Root");
        if (string.IsNullOrEmpty(root))
            root = "Thought";
        comboRoot.Text = root;
        //Refresh();
        Draw( false );
    }

    private void CollapseAll()
    {
        foreach (TreeViewItem item in theTreeView.Items)
            CollapseTreeviewItems(item);
    }

    //recursively collapse all the children
    private void CollapseTreeviewItems(TreeViewItem Item)
    {
        Item.IsExpanded = false;

        foreach (TreeViewItem item in Item.Items)
        {
            item.IsExpanded = false;

            if (item.ItemCount > 0 )
                CollapseTreeviewItems(item);
        }
    }
    private void Dlg_Loaded(object sender, RoutedEventArgs e)
    {
        ModuleUKS parent = (ModuleUKS)ParentModule;
        LoadRootHistory(parent.GetSavedDlgAttribute("RootHistory"));
        comboRoot.Text = parent.GetSavedDlgAttribute("Root");
    }


    private async void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        SetStatus("");

        var path = await Utils.OpenFileDialog( this, Utils.TitleBrainSimImport, Utils.FilterTextFile );

        if (string.IsNullOrEmpty(path)) return;

        if (string.IsNullOrWhiteSpace(path))
        {
            SetStatus("Choose a file first.");
            return;
        }
        if (!File.Exists(path))
        {
            SetStatus("File not found.");
            return;
        }

        try
        {
            ModuleUKS parent = (ModuleUKS)base.ParentModule;
            // Run ingest off the UI thread to keep the window responsive
            await Task.Run(() => theUKS.ImportTextFile(path));

            SetStatus("Success");
        }
        catch (Exception ex)
        {
            //Mouse.OverrideCursor = null;
            MessageBox.Alert( "Import failed.\n\n" + ex.Message, "UKS Import" );
        }
        finally
        {
            //Mouse.OverrideCursor = null;
        }
    }

    private async void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        var path = await Utils.SaveFileDialog( this, Utils.TitleBrainSimExport, Utils.FilterTextFile );


        if (string.IsNullOrEmpty(path)) return;

        if (string.IsNullOrWhiteSpace(path))
        {
            SetStatus("Choose a file first.");
            return;
        }
        try
        {
            ModuleUKS parent = (ModuleUKS)base.ParentModule;
            //get the root to save the contents of from the UKS dialog root
            string root = parent.GetSavedDlgAttribute("Root");
            await Task.Run(() => theUKS.ExportTextFile(root, path));
            SetStatus("Success");
        }
        catch (Exception ex)
        {
            // RHC - Need better alert 
            MessageBox.Alert( "Import failed.\n\n" + ex.Message, "UKS Export" );
        }
        finally
        {
        }
    }

    private void LoadRootHistory(string raw)
    {
        _rootHistory.Clear();
        if (!string.IsNullOrEmpty(raw))
            _rootHistory.AddRange(raw.Split('|').Where(x => !string.IsNullOrWhiteSpace(x)));
        UpdateRootHistoryItems();
    }

    private void AddRootToHistory(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var existing = _rootHistory.FindIndex(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0) _rootHistory.RemoveAt(existing);
        _rootHistory.Insert(0, value);
        if (_rootHistory.Count > rootHistoryLimit)
            _rootHistory.RemoveRange(rootHistoryLimit, _rootHistory.Count - rootHistoryLimit);
        UpdateRootHistoryItems();
        if (ParentModule is ModuleUKS parent)
            parent.SetSavedDlgAttribute("RootHistory", string.Join("|", _rootHistory));
    }

    private void UpdateRootHistoryItems()
    {
        _isTextChangingInternally = true;
        comboRoot.ItemsSource = null;
        comboRoot.ItemsSource = _rootHistory.ToList();
        _isTextChangingInternally = false;
    }

    private static bool IsDarkMode()
    {
#if WINDOWS
        const string personalize = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        object? value = Registry.GetValue(personalize, "AppsUseLightTheme", 1);
        return value is int i && i == 0;
#else
        return false;
#endif
    }

    private void ApplyContextMenuTheme(ContextMenu menu)
    {
        if (menu is null || !IsDarkMode()) return;

        var bg = new SolidColorBrush(Color.FromRgb(45, 45, 48));
        var fg = Brushes.White;
        var disabled = Brushes.LightGray;

        menu.Background = bg;
        menu.Foreground = fg;

        foreach (var item in menu.Items.OfType<Control>())
        {
            if (item is MenuItem mi)
            {
                if (mi.IsEnabled == false)
                {
                    mi.IsEnabled = true;
                    mi.Focusable = false;
                    mi.StaysOpenOnClick = true;
                    mi.IsHitTestVisible = false;
                    mi.Foreground = disabled;
                }
                else
                {
                    mi.Foreground = fg;
                }
                mi.Background = bg;
                mi.BorderThickness = new Thickness(0);
                if (mi.Header is TextBox tbHeader)
                {
                    tbHeader.Background = bg;
                    tbHeader.Foreground = fg;
                    tbHeader.BorderBrush = bg;
                    tbHeader.CaretBrush = fg;
                    tbHeader.SelectionBrush = fg;
                }
            }
        }
    }
}