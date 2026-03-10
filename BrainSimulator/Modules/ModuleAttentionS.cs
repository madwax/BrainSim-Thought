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
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.DirectoryServices.ActiveDirectory;
using System.Linq;
using System.Windows.Forms;
using UKS;

namespace BrainSimulator.Modules;

public class ModuleAttentionS : ModuleBase
{
    private readonly List<Thought> _sequenceBuffer = new();
    private DateTime _lastEventTime = DateTime.MinValue;
    SeqElement currentSeq = null;
    List<Thought> prediction = new();

    public override void Fire()
    {
        Init();

        var mm = GetMentalModel();

        foreach (Link l in ((Thought)"activeThought").LinksFrom.Where(x=>x.LinkType.Label== "is-a"))
        {
            if (l.HasLink(l,"handled", null) is not null) continue; //only handle this event once
            l.AddLink("handled", null);

            Thought theNote = l.From;
            if (prediction.Contains(theNote)) //raise well-being;
                ModuleWellBeing.Increase();
            else if (prediction.Count > 0)
                ModuleWellBeing.Decrease();

            //Add the new node to the phrase sequence
            if (currentSeq is null)
            {
                theUKS.GetOrAddThought("phrase", "musicalPhrase");
                currentSeq = theUKS.CreateFirstElement("phrase", theNote);
            }
            else
            {
                AddDurationToSeqStep();
                currentSeq = theUKS.AddElement(currentSeq, theNote);
            }
            _lastEventTime = DateTime.Now;

            // After each note-in, try to predict the next note based on known phrases
            prediction = PredictNextNote();
        }

        //Followin needed to store note durations
        //foreach (Link l in ((Thought)"inActiveThought").LinksFrom.Where(x => x.LinkType.Label == "is-a"))
        //{
        //    if (l.HasLink(l, "handled", null) is not null) continue; //only handle this event once
        //    l.AddLink("handled", null);
        //    if (currentSeq is null) continue; //never start a sequence with a note-end
        //    AddDurationToSeqStep();
        //    currentSeq = theUKS.AddElement(currentSeq, l.From);
        //    Debug.WriteLine($"added end event {l.From}");
        //    _lastEventTime = DateTime.Now;
        //}

        //save the sequence as a phrase
        if (_lastEventTime != DateTime.MinValue &&
          DateTime.Now - _lastEventTime >= TimeSpan.FromSeconds(2) && currentSeq is not null)
        {
            var theFlattenedSequence = theUKS.FlattenSequence(currentSeq.FRST);
            //does this sequence already exist?
            var existing = theUKS.RawSearchExact(theFlattenedSequence);
            if (existing.Count < 2)
            {
                theUKS.GetOrAddThought("musicalPhrase");
                theUKS.GetOrAddThought("soundAs", "LinkType");
                var newThought = theUKS.GetOrAddThought("phrase*", "musicalPhrase").AddLink("soundAs", currentSeq.FRST);
                int totalTime = 5000;
                foreach (SeqElement t in theUKS.EnumerateSequenceElements(currentSeq.FRST))
                    totalTime += ModuleSoundOut.GetDurationMs(t) * 3;
                newThought.From.TimeToLive = TimeSpan.FromMilliseconds(totalTime);
            }
            else
            {
                var foundPhrase = existing[0].seqNode.LinksFrom.FindFirst(x => x.LinkType.Label == "soundAs")?.From;
                theUKS.DeleteSequence(currentSeq.FRST);
            }
            currentSeq = null;
            prediction.Clear();
            _lastEventTime = DateTime.MinValue;
        }

        UpdateDialog();
    }

    private void AddDurationToSeqStep()
    {
        TimeSpan delta = _lastEventTime == DateTime.MinValue
            ? TimeSpan.Zero
            : DateTime.Now - _lastEventTime;
        var dt = theUKS.GetOrAddThought($"dt:{(int)delta.TotalMilliseconds}", "duration"); // Keep duration helper intact
        currentSeq.AddLink("duration", dt);
    }

    public override void Initialize()
    {
        _sequenceBuffer.Clear();
        _lastEventTime = DateTime.MinValue;
    }

    public override void UKSInitializedNotification()
    {
        EnsureSequenceRoots();
        theUKS.GetOrAddThought("do", "Action");
        _lastEventTime = DateTime.MinValue;
    }

    private void EnsureSequenceRoots()
    {
        theUKS.GetOrAddThought("handled", "LinkType");
        theUKS.GetOrAddThought("duration", "LinkType");
    }

    private List<Thought> PredictNextNote()
    {
        if (currentSeq?.FRST is null) return null;

        List<Thought> retVal = new();
        List<Thought> played = theUKS.FlattenSequence(currentSeq.FRST);
        if (played.Count == 0) return null;

        // Find all known phrases that start with the current sequence
        Thought predicted = null;
        var foundSequences = theUKS.HasSequence(played, "soundAs");
        if (foundSequences.Count > 0)
        {
            foreach (var foundSequence in foundSequences)
            {
                var sequenceContent = theUKS.FlattenSequence(foundSequence.seqNode);
                for (int i = 0; i < played.Count && i < sequenceContent.Count; i++)
                {
                    if (sequenceContent[i] != played[i]) goto misMatch;
                }
                if (sequenceContent.Count > played.Count)
                    predicted = sequenceContent[played.Count];
            misMatch: continue;
            }
            if (predicted != null)
                retVal.Add(predicted);
        }
        return retVal;
    }


    private ModuleMentalModel GetMentalModel()
    {
        return MainWindow.theWindow?.activeModules.OfType<ModuleMentalModel>().FirstOrDefault();
    }
    private ModuleWellBeing GetWellBeing()
    {
        return MainWindow.theWindow?.activeModules.OfType<ModuleWellBeing>().FirstOrDefault();
    }
}