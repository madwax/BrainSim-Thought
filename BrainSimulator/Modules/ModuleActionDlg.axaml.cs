using System.Windows;
using UKS;

namespace BrainSimulator.Modules;

public partial class ModuleActionDlg : ModuleBaseDlg
{
    public ModuleActionDlg()
    {
        InitializeComponent();
    }

    public override bool Draw(bool checkDrawTimer)
    {
        if (!base.Draw(checkDrawTimer)) return false;

        if (ParentModule is ModuleAction parent)
        {
            if (LastContextTextBox is not null)
                LastContextTextBox.Text = FormatThought(parent._lastContext);

            if (LastActionTextBox is not null)
                LastActionTextBox.Text = FormatThought(parent._lastSelectedAction);
        }

        return true;
    }

    private static string FormatThought(Thought thought)
    {
        if (thought is null) return "(none)";
        if (thought is Link link)
        {
            if (!string.IsNullOrWhiteSpace(link.To?.Label)) return link.To.Label;
            if (!string.IsNullOrWhiteSpace(link.Label)) return link.Label;
            return link.ToString();
        }

        return string.IsNullOrWhiteSpace(thought.Label) ? thought.ToString() : thought.Label;
    }

    private void Grid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        Draw(false);
    }
}