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


using NAudio.Midi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using UKS;
using static UKS.UKS;

namespace BrainSimulator.Modules;

public class ModuleSoundOut : ModuleBase
{

    DateTime lastFiredTime = DateTime.Now;
    DateTime? lastNotePressed = null;
    DateTime lastCadenceTime = DateTime.Now;
    List<Thought> tuneToSearch = null;

    private readonly HashSet<int> _pressedNotes = new();
    private readonly Dictionary<int, Thought> _noteInputs = new();
    private const int MinNote = 60; // C4
    private const int MaxNote = 76; // E5

    private const int MidiChannel = 1;
    private const int MidiVelocity = 100;
    private const int MidiPatch = 0; // Acoustic Grand Piano


    IEnumerator<SeqElement> enumerator = null;  //we'll needc multiple enumerators soon

    // Add these properties to the ModuleSound class
    public int Cadence { get; set; } = 100;
    public int PitchOffset { get; set; } = 0;

    public static MidiOut? midi;
    public static MidiOut Midi => midi ??= InitMidi();

    private static MidiOut InitMidi()
    {
        if ( ModuleSoundIn.midi != null)
        {
            midi = ModuleSoundIn.midi;
            return midi;
        }
        var m = new MidiOut(0);
        m.Send(MidiMessage.ChangePatch(MidiPatch, MidiChannel).RawData);
        return m;
    }

    public override void Fire()
    {
        Init();

        // fire held-note input thoughts and (re)bind them into the mental model
        //OLD search for phrase based on note sequence
        //if (lastNotePressed is not null && DateTime.Now > lastNotePressed + TimeSpan.FromMilliseconds(2000))
        //{
        //    if (tuneToSearch.Count > 1)
        //    {
        //        var tunesFound = theUKS.HasSequence(tuneToSearch, null, true);
        //        if (tunesFound.Count > 0)
        //        {
        //            var phrase = tunesFound[0].seqNode.LinksFrom.FindFirst(x => x.LinkType.Label == "soundAs")?.From;
        //            phrase.Fire();
        //        }
        //    }
        //    tuneToSearch = null;
        //    lastNotePressed = null;
        //}

        var muscialPhrase = ((Thought)"MusicalPhrase");
        if (muscialPhrase is not null)
            foreach (Link phrase in muscialPhrase.LinksFrom.Where(x => x.Label != "is-a"))
            {
                if (phrase.From.LastFiredTime < lastFiredTime) continue;
                if (!phrase.From.HasAncestor("Do")) continue;
                phrase.From.RemoveParent("Do");

                if (phrase.From.Label.StartsWith("phrase"))
                {
                    SeqElement seqStart = (SeqElement) phrase.From.LinksTo.FindFirst(x => x.LinkType.Label == "soundAs")?.To;
                    PlayThePhrase(seqStart);
                }
                else //old version without durations
                {
                    var seqStart = phrase.From.LinksTo.FindFirst(x => x.LinkType.Label == "soundAs")?.To;
                    if (!theUKS.IsSequenceElement(seqStart))
                        seqStart = phrase.LinksTo.FindFirst(x => theUKS.IsSequenceElement(x.To))?.To;

                    enumerator = theUKS.EnumerateSequenceElements(seqStart as SeqElement).GetEnumerator();
                }
            }

        if (DateTime.Now > lastCadenceTime + TimeSpan.FromMilliseconds(Cadence) && enumerator is not null)
        {
            lastCadenceTime = DateTime.Now;
            if (enumerator.MoveNext())
                theUKS.GetElementValue(enumerator.Current).Fire();
            else
                enumerator = null;
        }


        Thought musicalNote = "MusicalNoteOut";
        if (musicalNote is not null)
            foreach (var note1 in ((Thought)"MusicalNoteOut")?.Children)
            {
                if (note1.LastFiredTime <= lastFiredTime) continue;
                if (note1.Label == "+") continue;
                string pitch = note1.Label[5..];
                int note = pitch switch
                {
                    "C" => 60,
                    "D" => 62,
                    "E" => 64,
                    "F" => 65,
                    "G" => 67,
                    "A" => 69,
                    "B" => 71,
                    "C+" => 72,
                    _ => 60
                };
                note += PitchOffset;
                PlayNote(note);
            }

        lastFiredTime = DateTime.Now;
        UpdateDialog();
    }

    // Fill this method in with code which will execute once
    // when the module is added, when "initialize" is selected from the context menu,
    // or when the engine restart button is pressed
    public override void Initialize()
    {
    }

    private CancellationTokenSource _phraseCts;
    private Task _phraseTask;

    private void PlayThePhrase (SeqElement start)
    {
        // cancel any current phrase playback
        _phraseCts?.Cancel();
        _phraseTask = null;

        if (start is null) return;

        _phraseCts = new CancellationTokenSource();
        _phraseTask = RunPhraseAsync(start, _phraseCts.Token);
    }

    private async Task RunPhraseAsync(SeqElement start, CancellationToken token)
    {
        var enumerator = theUKS.EnumerateSequenceElements(start).GetEnumerator();
        try
        {
            while (!token.IsCancellationRequested && enumerator.MoveNext())
            {
                SeqElement elem = enumerator.Current;
                Thought value = theUKS.GetElementValue(elem);
                if (value?.Label is { } lbl && lbl.StartsWith("noteInput:", StringComparison.OrdinalIgnoreCase))
                    if (int.TryParse(lbl.AsSpan(10), out int noteNum))
                    {
//                        PlaceNoteInMentalModel(noteNum, value);
                        _ = Task.Delay(500).ContinueWith(_ =>
                        {
//                            ClearNoteFromMentalModel(noteNum);
                        });
                        PlayNote(noteNum);
                    }
                value?.Fire();

                int delayMs = GetDurationMs(elem);
                if (delayMs <= 0) delayMs = Cadence;
                if (delayMs > 0)
                    await Task.Delay(delayMs, token);
            }
        }
        catch (OperationCanceledException)
        {
            // ignored
        }
        finally
        {
            enumerator.Dispose();
        }
    }

    public static int GetDurationMs(SeqElement elem)
    {
        var link = elem?.LinksTo.FindFirst(x => x.LinkType?.Label == "duration");
        var dtThought = link?.To;
        if (dtThought?.Label is { } lbl && lbl.StartsWith("dt:", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(lbl.AsSpan(3), out int ms))
                return ms;
        }
        return 0;
    }

    // called whenever the UKS performs an Initialize()
    public override void UKSInitializedNotification()
    {
        GetUKS();

        theUKS.GetOrAddThought("MusicalPhrase");
        theUKS.GetOrAddThought("MusicalNoteOut", "MusicalPhrase");
        theUKS.GetOrAddThought("MusicalNoteIn", "MusicalPhrase");
  //      EnsureNoteInputs();
        theUKS.GetOrAddThought("pitchC", "MusicalNoteOut");
        theUKS.GetOrAddThought("pitchD", "MusicalNoteOut");
        theUKS.GetOrAddThought("pitchE", "MusicalNoteOut");
        theUKS.GetOrAddThought("pitchF", "MusicalNoteOut");
        theUKS.GetOrAddThought("pitchG", "MusicalNoteOut");
        theUKS.GetOrAddThought("pitchA", "MusicalNoteOut");
        theUKS.GetOrAddThought("pitchB", "MusicalNote");
        theUKS.GetOrAddThought("pitchC+", "MusicalNoteOut");
        theUKS.GetOrAddThought("+", "MusicalNoteOut");

        var s = theUKS.GetOrAddThought("soundAs", "Action");

//        theUKS.AddSequence(theUKS.GetOrAddThought("Triad", "MusicalPhrase"), s, new List<Thought> { "pitchC", "+", "pitchE", "+", "pitchG", "+" });
        theUKS.AddSequence(theUKS.GetOrAddThought("ThreeBlindMice", "MusicalPhrase"), s, new List<Thought> { "pitchE", "+", "+", "pitchD", "+", "+", "pitchC", "+", "+", "+", "+", "+" });
        //theUKS.AddSequence(theUKS.GetOrAddThought("SeeHowTheyRun", "MusicalPhrase"), s, new List<Thought> { "pitchG", "+", "+", "pitchF", "+", "pitchF", "pitchE", "+", "+", "+", "+", "+" });
        //theUKS.AddSequence(theUKS.GetOrAddThought("TheyAllRanAfter", "MusicalPhrase"), s,
        //    new List<Thought> { "pitchG", "pitchC+", "+", "pitchC+", "pitchB", "pitchA", "pitchB", "pitchC+", "+", "pitchG", "pitchG", "+" });
        //theUKS.AddSequence(theUKS.GetOrAddThought("TBL2", "MusicalPhrase"), s, new List<Thought> { "ThreeBlindMice-seq0", "ThreeBlindMice-seq0" });
        //theUKS.AddSequence(theUKS.GetOrAddThought("SHTR2", "MusicalPhrase"), s, new List<Thought> { "SeeHowTheyRun-seq0", "SeeHowTheyRun-seq0" });
        //theUKS.AddSequence(theUKS.GetOrAddThought("TARA3", "MusicalPhrase"), s, new List<Thought> { "TheyAllRanafter-seq0", "TheyAllRanafter-seq0", "TheyAllRanafter-seq0" });
        //theUKS.AddSequence(theUKS.GetOrAddThought("TBLSong", "MusicalPhrase"), s,
        //    new List<Thought> { "TBL2-seq0", "SHTR2-seq0", "TARA3-seq0", "+", "pitchF", "ThreeBlindMice-seq0" });

        lastFiredTime = DateTime.Now;
    }


    public void FireNote(string noteLabel)
    {
        Thought note = "pitch" + noteLabel;
        if (tuneToSearch == null) tuneToSearch = new();
        tuneToSearch.Add(note);
        lastNotePressed = DateTime.Now;
        if (note is not null)
            note.Fire();
    }

    public async void PlayCMajorTriad(int durationMs = 300)
    {
        int channel = 1;
        int velocity = 100;

        // C major triad: C4, E4, G4
        int c = 60; // C4
        int e = 64; // E4
        int g = 67; // G4

        // Optional: choose instrument (piano)
        Midi.Send(MidiMessage.ChangePatch(0, channel).RawData);

        // Start notes
        Midi.Send(MidiMessage.StartNote(c, velocity, channel).RawData);
        Midi.Send(MidiMessage.StartNote(e, velocity, channel).RawData);
        Midi.Send(MidiMessage.StartNote(g, velocity, channel).RawData);

        await Task.Delay(durationMs);

        // Stop notes
        Midi.Send(MidiMessage.StopNote(c, 0, channel).RawData);
        Midi.Send(MidiMessage.StopNote(e, 0, channel).RawData);
        Midi.Send(MidiMessage.StopNote(g, 0, channel).RawData);
    }

    public void PlayNote(int midiNote, int durationMs = 500)
    {
        Midi.Send(MidiMessage.StartNote(midiNote, MidiVelocity, MidiChannel).RawData);
        _ = Task.Delay(durationMs).ContinueWith(_ =>
        {
            Midi.Send(MidiMessage.StopNote(midiNote, 0, MidiChannel).RawData);
        });
    }
}
