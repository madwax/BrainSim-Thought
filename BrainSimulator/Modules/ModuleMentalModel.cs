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
using System.Linq;
using System.Security.Policy;
using System.Text.RegularExpressions;
using UKS;

namespace BrainSimulator.Modules;

public class ModuleMentalModel : ModuleBase
{
    // Root marker (so you can find all mental-model nodes quickly)
    public Thought Root { get; private set; }

    // Spatial sheet (fixed)
    //public SpatialSheet Sheet { get; private set; }
    public SpatialSheetConfig cfg;

    // Cells indexed by (ring, rayIndexWithinRing)
    public Thought[][] _cells = Array.Empty<Thought[]>();
    public Thought Center { get; private set; } = null!;

    // Internal link types (Thoughts used as LinkType identifiers, or however you model link types)
    private Thought _ltContains;
    private Thought _ltCenter;



    public override void Fire()
    {
        Init();

        UpdateDialog();
    }

    // Fill this method in with code which will execute once
    // when the module is added, when "initialize" is selected from the context menu,
    // or when the engine restart button is pressed
    public override void Initialize()
    {
    }

    // called whenever the UKS performs an Initialize()
    public override void UKSInitializedNotification()
    {
        theUKS = MainWindow.theUKS;

        Root = theUKS.GetOrAddThought("_mm:root");
        _ltContains = theUKS.GetOrAddThought("_mm:contains");
        _ltCenter = theUKS.GetOrAddThought("_mm:center");

        cfg = new SpatialSheetConfig();
        BuildOrLoad(cfg);
    }

    /// <summary>
    /// Bind an object Thought into one or more cells (distributed occupancy).
    /// Volatile links should decay quickly elsewhere in your tick/decay mechanism.
    /// </summary>
    public void BindObjectToCells(Thought obj, IEnumerable<Thought> cells, float weight = 1f)
    {
        foreach (var cell in cells)
        {
            Link l = cell.AddLink(_ltContains, obj);
            l.Weight = weight; // mark as volatile in your implementation
            l.TimeToLive = TimeSpan.FromSeconds(15);
        }
    }
    public Thought GetCell(Angle azimuth, Angle elevation)
    {
        //first, find which elevation band we're in
        // Clamp to valid range
        float el = (float)Math.Clamp(elevation.Degrees, -90.0, 90.0);

        int ring = GetBinFromElevation(el);

        //Then calculate the offset into that band and return the cell
        //wrap to range
        int idx =  GetBinFromAzimuth(azimuth, ring);
        return _cells[ring][idx];
    }

    private int GetBinFromAzimuth(Angle azimuth, int ring)
    {
        double x = azimuth.Degrees % 360.0;
        if (x >= 180.0) x -= 360.0;
        if (x < -180.0) x += 360.0;
        // normalize to [-1, +1]
        x = x / 180.0;

        // non-linear warp (more resolution near 0)
        float alpha = cfg.FrontBias; //1.5 mild, 5 extreme
        double u = Math.Tanh(alpha * x) / Math.Tanh(alpha); // still in [-1, +1]
        int bins = _cells[ring].Length;
        // map to [0, bins)
        double t = (u + 1.0) * 0.5;
        int idx = (int)Math.Floor(t * bins);
        if (idx < 0) idx = 0;
        if (idx >= bins) idx = bins - 1;
        return idx;
    }
    public Angle GetAzimuthFromBin(int bin, int ring)
    {
        float halfWidth = _cells[ring].Length / 2;
        float offset = bin - halfWidth;
        float u = offset / halfWidth;
        Angle a = Angle.FromDegrees((float)InvertWarp(u) * 180);
        return a;
    }
    public double InvertWarp(double u)
    {
        float alpha = cfg.FrontBias;
        // guard domain: |u| <= 1
        u = Math.Clamp(u, -1.0, 1.0);
        double v = u * Math.Tanh(alpha);      // scale back
        double x = Math.Atanh(v) / alpha;     // invert tanh and scale
        return Math.Clamp(x, -1.0, 1.0);
    }


    public int GetBinFromElevation(float el)
    {
        float absEl = Math.Abs(el);
        var edges = cfg.ElevationEdges;
        int absRing = edges.Count - 2; // default to last band

        for (int i = 0; i < edges.Count - 1; i++)
        {
            if (absEl < edges[i + 1])
            {
                absRing = i;
                break;
            }
        }

        int signedRing = el < 0 ? -absRing : absRing;
        return signedRing + cfg.Rings;
    }

    public void RotateMentalModel(Angle azimuth, Angle elevation)
    {
        Thought allCells = theUKS.Labeled("_mm:cell");
        if (allCells is null) return;
        foreach (var cell in allCells.Children)
        {
            foreach (Link l in cell.LinksTo.Where(x => x.LinkType == _ltContains))
            {
                //get the current source (bins) for this link.source
                Thought oldPosition = l.From;
                var angles = GetAnglesFromCell(oldPosition);
                angles.azimuth += azimuth;
                angles.elevation += elevation;
                Thought newPosition = GetCell(angles.azimuth, angles.elevation);
                if (newPosition != oldPosition)
                {
                    oldPosition.RemoveLink(l);
                    Link newLink = newPosition.AddLink(_ltContains, l.To);
                    newLink.TimeToLive = l.TimeToLive;
                }
            }
        }
    }

    /// <summary>
    /// Inverse of GetBinFromElevation: returns the band center elevation for a bin.
    /// Uses midpoints of the same absolute thresholds, mirrored for lower bins.
    /// </summary>
    public Angle GetElevationFromBin(int bin)
    {
        int clamped = Math.Clamp(bin, 0, _cells.Length - 1);
        int signedRing = clamped - cfg.Rings;
        int absRing = Math.Abs(signedRing);

        var edges = cfg.ElevationEdges;
        absRing = Math.Min(absRing, edges.Count - 2);

        float center = absRing == 0 ? 0f : (edges[absRing] + edges[absRing + 1]) * 0.5f;
        float deg = signedRing < 0 ? -center : center;
        return Angle.FromDegrees(deg);
    }

    (Angle azimuth, Angle elevation) GetAnglesFromCell(Thought t)
    {
        //string label = $"_mm:cell:r{r}:k{k}";
        var m = Regex.Match(t.Label, @"^_mm:cell:r(?<r>-?\d+):k(?<k>\d+)$");
        int r = m.Success ? int.Parse(m.Groups["r"].Value) : -1;
        int k = m.Success ? int.Parse(m.Groups["k"].Value) : -1;
        Angle elevation = GetElevationFromBin(r);
        Angle azimuth = GetAzimuthFromBin(k, r);
        return (azimuth, elevation);
    }

    public void BuildOrLoad(SpatialSheetConfig cfg)
    {
        // If you persist the sheet in the UKS, you can "load" by looking up cell labels.
        // For now, build deterministically; repeated builds will GetOrAdd the same cells.
        Build(cfg);
        LinkNeighbors(cfg);
    }

    private void Build(SpatialSheetConfig cfg)
    {
        int totalRings = cfg.Rings * 2 + 1;           // rings above + center + rings below
        int centerIndex = cfg.Rings;                  // center row index

        _cells = new Thought[totalRings][];
        Thought rootCells = theUKS.GetOrAddThought("_mm:cell", "_mm:root");

        for (int r = 0; r < totalRings; r++)
        {
            int signedRing = r;         // negative above, positive below, 0 = center
            if (signedRing > 6) 
                signedRing = signedRing - 2*Math.Abs(6-signedRing);
            int radialDistance = Math.Abs(signedRing);

            int rays = cfg.RaysPerRing(radialDistance);
            _cells[r] = new Thought[rays];

            for (int k = 0; k < rays; k++)
            {
                string label = $"_mm:cell:r{r}:k{k}";
                Thought cell = theUKS.GetOrAddThought(label, "_mm:cell");

                rootCells.AddLink("_mm:hasCell", cell);
                _cells[r][k] = cell;
            }
        }

        Thought[] centerRing = _cells[centerIndex];
        int centerRay = centerRing.Length > 0 ? centerRing.Length / 2 : 0;
        Center = centerRing[centerRay];

        rootCells.AddLink(_ltCenter, Center);
        Center.AddLink(_ltCenter, rootCells); // optional symmetry
    }

    private void LinkNeighbors(SpatialSheetConfig cfg)
    {
        // 1) Ring neighbors (wrap-around within each ring)
        Thought rt = theUKS.GetOrAddThought("rightOf", "comparison");
        Thought above = theUKS.GetOrAddThought("above", "comparison");
        for (int r = 0; r < _cells.Length; r++)
        {
            int rays = _cells[r].Length;
            for (int k = 0; k < rays; k++)
            {
                var a = _cells[r][k];
                var b = _cells[r][(k + 1) % rays];
                b.AddLink(rt, a);
            }
        }

        // 2) ring neighbors (connect ring r to ring r+1)
        for (int r = 0;r < cfg.Rings;r++)
        {
            //start with the densest row and work both upward and downward
            int denseRow1 = cfg.Rings - r;
            int denseRow2 = cfg.Rings + r;

            float numCellsInDenseRow = _cells[denseRow2].Length;
            float numCellsInLessDenseRow = _cells[denseRow2+1].Length;

            // Map each outer ray to an inner ray (or vice versa).
            // This is where non-linear geometry lives.
            for (int rowIndex = 0; rowIndex < numCellsInDenseRow; rowIndex++)
            {
                Thought cell1 = _cells[denseRow1][rowIndex];
                float pos2 = (rowIndex * numCellsInLessDenseRow)  / numCellsInDenseRow;
                Thought cell2 = _cells[denseRow1-1][(int)Math.Round(pos2)];
                cell2.AddLink(above, cell1);

                Thought cell3 = _cells[denseRow2][rowIndex];
                Thought cell4 = _cells[denseRow2+1][(int)Math.Round(pos2)];
                cell3.AddLink(above, cell4);
            }
        }
    }
}

//this allows for a non-linear spatial sheet where the number of rays can grow with each ring,
//and a front bias can allocate more cells to the front sector if desired.
public sealed class SpatialSheetConfig
{
    public int Rings { get; init; } = 6;

    // Base rays and growth control how “non-linear” it is.
    public int BaseRays { get; init; } = 16;
    public float Growth { get; init; } = 1.35f;

    // Optional: front bias factor ( >1 means more cells allocated to “front sector”)
    // We'll implement anisotropy later; for now it influences RaysPerRing.
    public float FrontBias { get; init; } = 1.0f;

    // Optional: Elevation edges for flexible banding
    public List<float> ElevationEdges { get; init; } = new() { 0f, 1.5f, 5f, 10f, 20f, 35f, 60f, 90f };

    public int RaysPerRing(int ring)
    {
        // Non-linear: rays grow sublinearly or superlinearly; tune later.
        // This is a placeholder that’s easy to adjust.
        double rays = BaseRays * Math.Pow(Growth, ring);

        // Optionally clamp
        int r = (int)Math.Round(rays);
        return Math.Clamp(r, 8, 128);
    }
}
