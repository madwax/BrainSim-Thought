using BrainSimulator;
using BrainSimulator.Modules;
using UKS;
using Xunit;

namespace BrainSimulator.Tests;

public class ModuleMentelModelTests
{
    private static UKS.UKS CreateUKS()
    {
        var uks = new UKS.UKS(clear: true);
        uks.CreateInitialStructure();
        MainWindow.theUKS = uks; // provide the static used by modules
        return uks;
    }

    [Fact]
    public void RotateMentalModel_MovesContainsLinkToNewCell()
    {
        // arrange
        var uks = CreateUKS();
        var module = new ModuleMentalModel();
        module.UKSInitializedNotification();

        Thought obj = uks.GetOrAddThought("obj");
        Thought startCell = module.GetCell(Angle.FromDegrees(0), Angle.FromDegrees(0));
        module.BindObjectToCells(obj, new[] { startCell });

        // ensure the loop in RotateMentalModel will see this cell
        startCell.AddLink("is-a", module.Root);

        Link contains = startCell.LinksTo.First(l => l.LinkType?.Label == "_mm:contains");

        // act: rotate slightly to the right on the horizontal axis
        module.RotateMentalModel(Angle.FromDegrees(20), Angle.FromDegrees(20));

        // assert
        Thought expectedCell = module.GetCell(Angle.FromDegrees(20), Angle.FromDegrees(20));
        Assert.Same(expectedCell, contains.From);
    }
}