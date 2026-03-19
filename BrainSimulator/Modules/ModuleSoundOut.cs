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
        lastFiredTime = DateTime.Now;
        UpdateDialog();
    }

    // Fill this method in with code which will execute once
    // when the module is added, when "initialize" is selected from the context menu,
    // or when the engine restart button is pressed
    public override void Initialize()
    {
    }
    public void PlayThePhrase(Thought phrase)
    {
        SeqElement seqStart = (SeqElement)phrase.GetTargetOfFirstLinkOfType("soundAs");
        PlayThePhrase(seqStart);
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
                        PlayNote(noteNum);
                    }
                //value?.Fire();

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
        var s = theUKS.GetOrAddThought("soundAs", "Action");
        lastFiredTime = DateTime.Now;
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

    bool listenToOutput = true;
    public void PlayNote(int midiNote, int durationMs = 500)
    {
        var listener = MainWindow.theWindow?.activeModules.OfType<ModuleSoundIn>().FirstOrDefault();
        if (listenToOutput)
            listener.StartNote(midiNote);
        Midi.Send(MidiMessage.StartNote(midiNote, MidiVelocity, MidiChannel).RawData);
        _ = Task.Delay(durationMs).ContinueWith(_ =>
        {
            if (listenToOutput)
                listener.StopNote(midiNote);
            Midi.Send(MidiMessage.StopNote(midiNote, 0, MidiChannel).RawData);
        });
    }
}
