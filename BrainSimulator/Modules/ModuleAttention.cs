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
using UKS;
using static System.Math;

namespace BrainSimulator.Modules;

public class ModuleAttention : ModuleBase
{
    // Center of attention in degrees (owned by this module, used to set sliders)
    public Angle CenterAzimuthDeg = new();
    public Angle CenterElevationDeg = new();

    private readonly Random _rng = new();
    private Thought _lastFocus;
    public Thought CurrentFocus { get; private set; }

    // Queue and prediction roots in UKS space (volatile)
    private Thought _queueRoot;
    private Thought _predRoot;
    private Thought _ltQueued;
    private Thought _ltQueueFor;
    private Thought _ltPredItem;
    private Thought _ltPredFrom;
    private Thought _ltPredTo;

    private const float SoftmaxTemperature = 0.35f;
    private const float SameTargetPenalty = 0.6f;
    private const float ActivationBoost = 0.2f;
    private const float ActivationBoostMax = 5f;

    private const float wNovelty = 0.35f;
    private const float wActivation = 0.25f;
    private const float wSurprise = 0.2f;
    private const float wProximity = 0.25f;
    private const float wHabituation = 0.25f;
    private const float ActivationHalfLifeSeconds = 5.0f;
    private const float HabituationDenominator = 6.0f;
    private const float SalienceFloor = 0.05f;
    private const float AttentionStaleSeconds = 15.0f;

    private Thought _attentionCell;

    public ModuleAttention()
    {
        Label = "Attention";
    }

    public override void Fire()
    {
        Init();
        //EnsureLinkTypes();
        FireAttention();
        UpdateAttentionPositionInMentalModel(CurrentFocus);
        UpdateDialog();
    }

    public override void Initialize()
    {
        _attentionCell = null;
        SetCenterOfAttention(0, 0);
    }

    public void SetCenterOfAttention(Angle azimuthDeg, Angle elevationDeg)
    {
        var mm = GetMentalModel();
        if (mm is null) return;

        CenterAzimuthDeg = azimuthDeg;
        CenterElevationDeg = elevationDeg;
        Thought targetCell = mm.GetCell(azimuthDeg, elevationDeg);
        Link l = mm.BindThoughtToMentalModel("attention", targetCell);
        l.TimeToLive = TimeSpan.MaxValue;
    }

    private void UpdateAttentionPositionInMentalModel(Thought focus = null)
    {
        var mm = GetMentalModel();
        if (mm is null) return;

        if (focus is null) return;


        Thought targetCell = ResolveAttentionCell(focus, mm);
        if (targetCell is null) return;

        theUKS.GetOrAddThought("attention", "Abstract");
        Link l = mm.BindThoughtToMentalModel("attention", targetCell);
        l.TimeToLive = TimeSpan.MaxValue;
        _attentionCell = targetCell;

    }

    private Thought ResolveAttentionCell(Thought focus, ModuleMentalModel mm)
    {
        // Prefer the cell containing the current focus
        Thought cell = GetCellForThought(focus);
        if (cell is null)
        {
            // Fall back to last known attention cell, or the mental model center
            cell = _attentionCell ?? mm.Center;
        }
        return cell;
    }

    private static Thought GetCellForThought(Thought t)
    {
        // Reverse of the "_mm:contains" link: object -> cell
        return t?.LinksFrom.FindFirst(x => x.LinkType?.Label == "_mm:contains")?.From;
    }

    private void FireAttention()
    {
        Thought focus = SelectNextFocus();
        if (focus is null)
        {
            CurrentFocus = null;
            return;
        }

        foreach (var pred in FindPredictionsFor(focus))
            ReinforcePrediction(pred);

        ClearPredictions();

        //focus.Fire();
        BoostActivation(focus);
        ActivateRelated(focus);

        BuildPredictions(focus);
        //foreach (var pred in EnumeratePredictionTargets())
        //    pred?.Fire();

        TouchQueueItem(focus);

        _lastFocus = focus;
        CurrentFocus = focus;
    }

    public Thought SelectNextFocus()
    {
        ModuleMentalModel mm = GetMentalModel();
        if (mm is null) return null;

        PruneAttentionQueue(DateTime.Now, mm);

        var pool = new List<(Thought item, Thought target, double weight)>();
        foreach (var (item, target) in EnumerateQueueItems())
        {
            float salience = ComputeSalience(target, mm, 0, item.UseCount, item.LastFiredTime);
            item.Weight = salience;
            double adjusted = salience;
            if (_lastFocus == target) adjusted *= SameTargetPenalty;

            double weight = Math.Exp(adjusted / Math.Max(SoftmaxTemperature, 1e-6));
            if (double.IsNaN(weight) || weight <= 0) continue;
            pool.Add((item, target, weight));
        }

        double total = pool.Sum(p => p.weight);
        if (total <= 0) return null;

        double roll = _rng.NextDouble() * total;
        double acc = 0;
        foreach (var entry in pool)
        {
            acc += entry.weight;
            if (roll <= acc)
                return entry.target;
        }
        return pool[^1].target;
    }

    public Thought AddToAttentionQueue(Thought t, double surprise = 0d)
    {
        if (t is null || t.Label == "attention") return null;
        ModuleMentalModel mm = GetMentalModel();
        EnsureLinkTypes();

        var item = FindQueueItemFor(t);
        if (item is null)
        {
            item = theUKS.GetOrAddThought("_attn:item:*", "attention");
            _queueRoot.AddLink(_ltQueued, item);
            item.AddLink(_ltQueued, t);
        }

        item.LastFiredTime = DateTime.Now;
        item.UseCount++;
        item.Weight = ComputeSalience(t, mm, surprise, item.UseCount, item.LastFiredTime);
        return item;
    }

    private void PruneAttentionQueue(DateTime now, ModuleMentalModel mm)
    {
        EnsureLinkTypes();
        foreach (var (item, target) in EnumerateQueueItems().ToList())
        {
            bool expired =
                target.TimeToLive != TimeSpan.MaxValue &&
                target.LastFiredTime + target.TimeToLive < now;

            double sal = ComputeSalience(target, mm, 0, item.UseCount, item.LastFiredTime);
            bool tooLow = sal < SalienceFloor;
            bool stale = (now - item.LastFiredTime).TotalSeconds > AttentionStaleSeconds;

            if (expired || tooLow || stale)
                RemoveQueueItem(item);
        }
    }

    private void TouchQueueItem(Thought focus)
    {
        return;
        var item = FindQueueItemFor(focus);
        if (item is null) return;
        item.LastFiredTime = DateTime.Now;
    }

    private IEnumerable<(Thought item, Thought target)> EnumerateQueueItems()
    {
        //EnsureLinkTypes();
        foreach (var link in _queueRoot.LinksTo.Where(x => x.LinkType == _ltQueued))
        {
            Thought item = link.To;
            Thought target = item?.LinksTo.FindFirst(x => x.LinkType == _ltQueued)?.To;
            if (item is null || target is null) continue;
            yield return (item, target);
        }
    }

    private Thought FindQueueItemFor(Thought t)
    {
        EnsureLinkTypes();
        foreach (var (item, target) in EnumerateQueueItems())
            if (ReferenceEquals(target, t))
                return item;
        return null;
    }

    private void RemoveQueueItem(Thought item)
    {
        if (item is null) return;
        foreach (var l in _queueRoot.LinksToWriteable.Where(x => x.To == item && x.LinkType == _ltQueued).ToList())
            _queueRoot.RemoveLink(l);
        item.Delete();
    }

    private float ComputeSalience(Thought t, ModuleMentalModel mm, double surprise, int seenCount, DateTime lastSeen)
    {
        return .5f;
        if (t is null || mm is null) return 0;
        double novelty = 1.0 / (1.0 + t.UseCount);
        double activation = ComputeActivation(t);
        double proximity = mm.ComputeProximity(t);
        double habituation = seenCount <= 0 ? 0 : seenCount / (seenCount + HabituationDenominator);
        double recencyPenalty = (DateTime.Now - lastSeen).TotalSeconds / AttentionStaleSeconds;
        recencyPenalty = Math.Max(0, recencyPenalty);

        double sal = wNovelty * novelty
                   + wActivation * activation
                   + wSurprise * surprise
                   + wProximity * proximity
                   - wHabituation * habituation
                   - 0.05 * recencyPenalty;
        return (float)Math.Max(0, sal);
    }

    private static float ComputeActivation(Thought t)
    {
        return 0.5f;
        double seconds = Math.Max(0, (DateTime.Now - t.LastFiredTime).TotalSeconds);
        return (float)Math.Exp(-seconds / ActivationHalfLifeSeconds);
    }

    private void BoostActivation(Thought t)
    {
        return;
        float boosted = t.Weight + ActivationBoost;
        t.Weight = (float)Math.Min(boosted, ActivationBoostMax);
    }

    private void ActivateRelated(Thought focus)
    {
        return;
        foreach (var link in focus.LinksTo.Where(x =>
                     x.LinkType?.Label is "is-a" or "hasAttribute" or "is" or "part-of" or "means"))
            link.To?.Fire();

        foreach (var link in focus.LinksFrom.Where(x =>
                     x.LinkType?.Label is "is-a" or "part-of" or "means"))
            link.From?.Fire();
    }

    private void BuildPredictions(Thought focus)
    {
        return;
        EnsureLinkTypes();
        ClearPredictions();

        foreach (var link in focus.LinksFrom.Where(x => x.LinkType?.Label == "VLU"))
        {
            if (link.From is not SeqElement elem) continue;
            SeqElement nxt = elem.NXT;
            Thought nextValue = nxt?.VLU;
            if (nextValue is null) continue;

            Thought predItem = theUKS.GetOrAddThought("_attn:pred:*", "attention");
            _predRoot.AddLink(_ltPredItem, predItem);
            predItem.AddLink(_ltPredFrom, elem);
            predItem.AddLink(_ltPredTo, nextValue);
            predItem.TimeToLive = TimeSpan.FromSeconds(5);
        }
    }

    private IEnumerable<(SeqElement source, Thought next)> FindPredictionsFor(Thought focus)
    {
        EnsureLinkTypes();
        foreach (var link in _predRoot.LinksTo.Where(x => x.LinkType == _ltPredItem).ToList())
        {
            Thought pred = link.To;
            Thought next = pred?.LinksTo.FindFirst(x => x.LinkType == _ltPredTo)?.To;
            Thought src = pred?.LinksTo.FindFirst(x => x.LinkType == _ltPredFrom)?.To;
            if (next == focus && src is SeqElement se)
                yield return (se, next);
        }
    }

    private IEnumerable<Thought> EnumeratePredictionTargets()
    {
        EnsureLinkTypes();
        foreach (var link in _predRoot.LinksTo.Where(x => x.LinkType == _ltPredItem))
        {
            Thought pred = link.To;
            Thought next = pred?.LinksTo.FindFirst(x => x.LinkType == _ltPredTo)?.To;
            if (next is not null) yield return next;
        }
    }

    private void ClearPredictions()
    {
        return;
        EnsureLinkTypes();
        foreach (var link in _predRoot.LinksTo.Where(x => x.LinkType == _ltPredItem).ToList())
        {
            Thought pred = link.To;
            _predRoot.RemoveLink(link);
            pred?.Delete();
        }
    }

    private static void ReinforcePrediction((SeqElement source, Thought next) pred)
    {
        return;
        pred.source.Fire();
        var nxtLink = pred.source.LinksToWriteable.FindFirst(x => x.LinkType?.Label == "NXT");
        nxtLink?.Fire();
        pred.source.NXT?.Fire();
        pred.next.Fire();
    }

    private ModuleMentalModel GetMentalModel()
    {
        return MainWindow.theWindow?.activeModules.OfType<ModuleMentalModel>().FirstOrDefault();
    }

    private void EnsureLinkTypes()
    {

        _queueRoot = theUKS.GetOrAddThought("attention", "Abstract");
        _queueRoot = theUKS.GetOrAddThought("_attn:queue", "attention");
        _predRoot = theUKS.GetOrAddThought("_attn:predRoot", "attention");
        _ltQueued = theUKS.GetOrAddThought("_attn:queued", "attention");
        _ltPredItem = theUKS.GetOrAddThought("_attn:predItem", "attention");
        _ltPredFrom = theUKS.GetOrAddThought("_attn:predFrom", "attention");
        _ltPredTo = theUKS.GetOrAddThought("_attn:predTo", "attention");
    }

    private sealed record PendingPrediction(SeqElement SourceElement, Thought NextValue);
}