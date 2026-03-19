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
using System.Linq;
using UKS;

namespace BrainSimulator.Modules;

public class ModuleWellBeing : ModuleBase
{
    private const float MinState = -1f;
    private const float MaxState = 1f;
    private static float _state = 0f; // -1 very sad, +1 very happy
    private static DateTime _lastUpdateUtc = DateTime.UtcNow;

    public static double DecayTimeConstantSeconds { get; set; } = 5f; // adjust as needed

    public static float State
    {
        get
        {
            ApplyDecay();
            return _state;
        }
    }

    private static void ApplyDecay()
    {
        var now = DateTime.UtcNow;
        var dt = (now - _lastUpdateUtc).TotalSeconds;
        _lastUpdateUtc = now;
        if (dt <= 0 || DecayTimeConstantSeconds <= 0) return;

        var decay = Math.Exp(-dt / DecayTimeConstantSeconds);
        _state *= (float)decay;
    }

    private static float SetStateInternal(float target, bool decayAlreadyApplied)
    {
        if (!decayAlreadyApplied) ApplyDecay();

        float clampedTarget = Math.Clamp(target, MinState, MaxState);
        float appliedDelta = clampedTarget - _state; // positive = better, negative = worse

        TimeSpan recency = TimeSpan.FromSeconds(Math.Abs(appliedDelta) * 200);
        //Thought.FireAllRecentlyFiredThoughts(recency);

        // Notify ModuleAction of the well-being change
        var moduleAction = MainWindow.theWindow?.activeModules.OfType<ModuleAction>().FirstOrDefault();
        moduleAction?.NewResult(appliedDelta);

        _state = clampedTarget;

        return _state;
    }

    public static float SetState(float value) => SetStateInternal(value, decayAlreadyApplied: false);

    // Asymptotic toward limits
    public static float Increase(float fraction = 0.05f)
    {
        ApplyDecay();
        float target = _state + (MaxState - _state) * fraction;
        return SetStateInternal(target, decayAlreadyApplied: true);
    }

    public static float Decrease(float fraction = 0.05f)
    {
        ApplyDecay();
        float target = _state + (MinState - _state) * fraction;
        return SetStateInternal(target, decayAlreadyApplied: true);
    }

    public override void Fire()
    {
        ApplyDecay();
        Init();
        UpdateDialog();
    }

    public override void Initialize()
    {
        _state = 0f;
        _lastUpdateUtc = DateTime.UtcNow;
    }

    public override void UKSInitializedNotification()
    {
    }
}