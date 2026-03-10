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

public class ModuleSoundIn : ModuleBase
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
        if (ModuleSoundOut.midi is not null)
        {
            midi = ModuleSoundOut.Midi;
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
        foreach (int note in _pressedNotes)
            if (_noteInputs.TryGetValue(note, out var t))
            {
                t.Fire();
                PlaceNoteInMentalModel(note, t);
            }

/*        //OLD search for phrase based on note sequence
        if (lastNotePressed is not null && DateTime.Now > lastNotePressed + TimeSpan.FromMilliseconds(2000))
        {
            if (tuneToSearch.Count > 1)
            {
                var tunesFound = theUKS.HasSequence(tuneToSearch, null, true);
                if (tunesFound.Count > 0)
                {
                    var phrase = tunesFound[0].seqNode.LinksFrom.FindFirst(x => x.LinkType.Label == "soundAs")?.From;
                    phrase.Fire();
                }
            }
            tuneToSearch = null;
            lastNotePressed = null;
        }

        var muscialPhrase = ((Thought)"MusicalPhrase");
        if (muscialPhrase is not null)
            foreach (Link phrase in muscialPhrase.LinksFrom.Where(x => x.Label != "is-a"))
            {
                if (phrase.From.LastFiredTime < lastFiredTime) continue;
                if (phrase.From.Label.StartsWith("phrase"))
                {
                    SeqElement seqStart = (SeqElement) phrase.From.LinksTo.FindFirst(x => x.LinkType.Label == "soundAs")?.To;
                    PlayThePhrase(seqStart);
                }
                else 
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

  */
        lastFiredTime = DateTime.Now;
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
        GetUKS();

        theUKS.GetOrAddThought("MusicalPhrase");
        theUKS.GetOrAddThought("MusicalNoteOut", "MusicalPhrase");
        theUKS.GetOrAddThought("MusicalNoteIn", "MusicalPhrase");
        EnsureNoteInputs();

        var s = theUKS.GetOrAddThought("soundAs", "Action");

        lastFiredTime = DateTime.Now;
        _pressedNotes.Clear();
    }

    private void EnsureNoteInputs()
    {
        _noteInputs.Clear();
        for (int note = MinNote; note <= MaxNote; note++)
        {
            var t = theUKS.GetOrAddThought($"noteInput:{note}", "MusicalNoteIn");
            _noteInputs[note] = t;
        }
    }

    public void StartNote(int midiNote)
    {
        Midi.Send(MidiMessage.StartNote(midiNote, MidiVelocity, MidiChannel).RawData);
        if (midiNote >= MinNote && midiNote <= MaxNote)
            _pressedNotes.Add(midiNote);
        //PlaceNoteInMentalModel Now handled in polling loop
    }

    public void StopNote(int midiNote)
    {
        Midi.Send(MidiMessage.StopNote(midiNote, 0, MidiChannel).RawData);
        _pressedNotes.Remove(midiNote);
        ClearNoteFromMentalModel(midiNote);
    }

    private ModuleMentalModel GetMentalModel()
    {
        return MainWindow.theWindow?.activeModules.OfType<ModuleMentalModel>().FirstOrDefault();
    }

    private void PlaceNoteInMentalModel(int midiNote, Thought noteThought)
    {
        var mm = GetMentalModel();
        if (mm is null || noteThought is null) return;

        float azDeg = (midiNote - 60) * 2.5f;
        var cell = mm.GetCell(Angle.FromDegrees(azDeg), Angle.FromDegrees(0));
        if (cell is not null)
            mm.BindThoughtToMentalModel(noteThought.Label, cell); // renew or add link
    }
    private void ClearNoteFromMentalModel(int midiNote)
    {
        var mm = GetMentalModel();
        if (mm is null) return;
        if (_noteInputs.TryGetValue(midiNote, out var t))
        {
            mm.UnbindThought(t);
        }
    }
}
