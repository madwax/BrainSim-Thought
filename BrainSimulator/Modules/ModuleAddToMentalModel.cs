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

using System;
using System.Linq;
using UKS;

namespace BrainSimulator.Modules;

public class ModuleAddToMentalModel : ModuleBase
{
    public override void Fire()
    {
        Init();
        UpdateDialog();
    }

    public override void Initialize()
    {
    }

    public override void UKSInitializedNotification()
    {
        GetUKS();
        theUKS.GetOrAddThought("distance", "LinkType");
    }

    public void AddThoughtToMentalModel(string thoughtLabel, double azimuthDeg, double elevationDeg, double distance)
    {
        GetUKS();
        if (string.IsNullOrWhiteSpace(thoughtLabel) || theUKS is null) return;

        var mm = MainWindow.theWindow?.activeModules.OfType<ModuleMentalModel>().FirstOrDefault();
        if (mm is null) return;

        Thought t = theUKS.GetOrAddThought(thoughtLabel);
        Thought cell = mm.GetCell(Angle.FromDegrees((float)azimuthDeg), Angle.FromDegrees((float)elevationDeg));
        if (cell is null) return;

        // Bind to mental model
        var link = mm.BindThoughtToMentalModel(t, cell);

        // Store distance as a link on the binding (or on the object if binding not available)
        Thought distanceThought = theUKS.GetOrAddThought($"distance:{distance}", "distance");
        if (link is not null)
            link.AddLink("distance", distanceThought);
        else
            t.AddLink("distance", distanceThought);
    }
}